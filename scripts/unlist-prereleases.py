"""Unlist every prerelease version of the AdoNet.Async packages on nuget.org.

nuget.org cannot delete a package version, only unlist it. An unlisted version is hidden from
search and from the package's version list, but a restore that pins it exactly still works.

Versions nuget.org already reports as unlisted are skipped, so a re-run only spends requests on
what is left. nuget.org limits how many unlists a key may do; when it starts refusing, the run
stops after a few consecutive refusals instead of spending the rest of the list on them, and a
later re-run picks up where this one stopped.

Usage: NUGET_API_KEY=... python scripts/unlist-prereleases.py [--apply]
Without --apply it only prints what it would unlist.
"""

import gzip
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

# Consecutive refusals after which the run stops: nuget.org is no longer accepting unlists.
MAX_CONSECUTIVE_REFUSALS = 5


def get_json(url):
    request = urllib.request.Request(url, headers={"User-Agent": USER_AGENT, "Accept-Encoding": "gzip"})
    with urllib.request.urlopen(request) as response:
        data = response.read()
        if response.headers.get("Content-Encoding") == "gzip":
            data = gzip.decompress(data)
    return json.loads(data)


def listed_prereleases(package_id):
    """The prerelease versions nuget.org still lists, from the package's registration pages."""
    index = get_json(f"https://api.nuget.org/v3/registration5-gz-semver2/{package_id.lower()}/index.json")
    result = []
    for page in index["items"]:
        items = page["items"] if "items" in page else get_json(page["@id"])["items"]
        for item in items:
            entry = item["catalogEntry"]
            if "-" in entry["version"] and entry.get("listed", True):
                result.append(entry["version"])
    return result


def unlist(package_id, version, api_key):
    """Returns the HTTP status and, for a failure, the response body."""
    url = f"https://www.nuget.org/api/v2/package/{package_id}/{version}"
    for attempt in range(6):
        request = urllib.request.Request(
            url, method="DELETE", headers={"X-NuGet-ApiKey": api_key, "User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, ""
        except urllib.error.HTTPError as error:
            if error.code == 429 or error.code >= 500:
                delay = int(error.headers.get("Retry-After") or 0) or 2 ** (attempt + 1)
                print(f"  {error.code}, retrying in {delay}s", flush=True)
                time.sleep(delay)
                continue
            body = error.read().decode("utf-8", "replace").strip()
            return error.code, f"{error.reason} {body}"[:500]
    return 429, "still rate limited after retries"


def main():
    apply = "--apply" in sys.argv[1:]
    api_key = os.environ.get("NUGET_API_KEY", "")
    if apply and not api_key:
        sys.exit("NUGET_API_KEY is not set")

    pending = {package_id: listed_prereleases(package_id) for package_id in PACKAGES}
    total = sum(len(v) for v in pending.values())
    for package_id, versions in pending.items():
        print(f"{package_id}: {len(versions)} prerelease versions still listed", flush=True)
    if not apply:
        print(f"would unlist {total} prerelease versions")
        return

    unlisted = 0
    refusals = 0
    for package_id, versions in pending.items():
        for version in versions:
            status, detail = unlist(package_id, version, api_key)
            if status in (200, 204):
                print(f"  unlisted {package_id} {version}", flush=True)
                unlisted += 1
                refusals = 0
            else:
                print(f"  FAILED {package_id} {version}: HTTP {status} {detail}", flush=True)
                refusals += 1
                if refusals >= MAX_CONSECUTIVE_REFUSALS:
                    print(f"stopping: {refusals} refusals in a row; re-run later to continue")
                    print(f"unlisted {unlisted}; {total - unlisted} still listed")
                    sys.exit(1)
            time.sleep(0.25)

    print(f"unlisted {unlisted} of {total} prerelease versions")
    if unlisted < total:
        sys.exit(1)


if __name__ == "__main__":
    main()
