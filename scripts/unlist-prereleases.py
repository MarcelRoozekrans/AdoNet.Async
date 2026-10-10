"""Unlist every prerelease version of the AdoNet.Async packages on nuget.org.

nuget.org cannot delete a package version, only unlist it. An unlisted version is hidden from
search and from the package's version list, but a restore that pins it exactly still works.

Usage: NUGET_API_KEY=... python scripts/unlist-prereleases.py [--apply]
Without --apply it only prints what it would unlist.
"""

import json
import os
import sys
import time
import urllib.error
import urllib.request

PACKAGES = [
    "AdoNet.Async",
    "AdoNet.Async.Adapters",
    "AdoNet.Async.DataSet",
    "AdoNet.Async.DataSet.Generator",
    "AdoNet.Async.Serialization.NewtonsoftJson",
    "AdoNet.Async.Serialization.SystemTextJson",
]

USER_AGENT = "AdoNet.Async-unlist-prereleases"


def versions(package_id):
    url = f"https://api.nuget.org/v3-flatcontainer/{package_id.lower()}/index.json"
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT})
    with urllib.request.urlopen(request) as response:
        return json.load(response)["versions"]


def unlist(package_id, version, api_key):
    url = f"https://www.nuget.org/api/v2/package/{package_id}/{version}"
    for attempt in range(6):
        request = urllib.request.Request(
            url, method="DELETE", headers={"X-NuGet-ApiKey": api_key, "User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(request) as response:
                return response.status
        except urllib.error.HTTPError as error:
            if error.code == 429 or error.code >= 500:
                delay = int(error.headers.get("Retry-After") or 0) or 2 ** (attempt + 1)
                print(f"  {error.code}, retrying in {delay}s", flush=True)
                time.sleep(delay)
                continue
            return error.code
    return 429


def main():
    apply = "--apply" in sys.argv[1:]
    api_key = os.environ.get("NUGET_API_KEY", "")
    if apply and not api_key:
        sys.exit("NUGET_API_KEY is not set")

    failed = []
    total = 0
    for package_id in PACKAGES:
        prereleases = [v for v in versions(package_id) if "-" in v]
        total += len(prereleases)
        print(f"{package_id}: {len(prereleases)} prerelease versions", flush=True)
        for version in prereleases:
            if not apply:
                print(f"  would unlist {version}")
                continue
            status = unlist(package_id, version, api_key)
            if status in (200, 204):
                print(f"  unlisted {version}", flush=True)
            else:
                print(f"  FAILED {version}: HTTP {status}", flush=True)
                failed.append(f"{package_id} {version} HTTP {status}")
            time.sleep(0.25)

    verb = "unlisted" if apply else "would unlist"
    print(f"{verb} {total - len(failed)} of {total} prerelease versions")
    if failed:
        print("failures:")
        for line in failed:
            print(f"  {line}")
        sys.exit(1)


if __name__ == "__main__":
    main()
