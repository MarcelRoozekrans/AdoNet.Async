"""Unlist every prerelease version of the AdoNet.Async packages on nuget.org.

nuget.org cannot delete a package version, only unlist it. An unlisted version is hidden from
search and from the package's version list, but a restore that pins it exactly still works.

Versions nuget.org already reports as unlisted are skipped, so a re-run only spends requests on
what is left. nuget.org caps a key's call volume and answers 403 "Quota Exceeded" with the time
until the quota is replenished; the run sleeps that long and carries on. Any other refusal stops
the run after a few in a row, and a later re-run picks up where it stopped.

Usage: NUGET_API_KEY=... python scripts/unlist-prereleases.py [--apply]
Without --apply it only prints what it would unlist.
"""

import gzip
import json
import re
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

# How many times one version may wait for the quota to be replenished before it counts as refused.
MAX_QUOTA_WAITS = 3

QUOTA_RESET = re.compile(r"replenished in (\d+):(\d+):(\d+)")


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
    retries = 0
    quota_waits = 0
    while True:
        request = urllib.request.Request(
            url, method="DELETE", headers={"X-NuGet-ApiKey": api_key, "User-Agent": USER_AGENT})
        try:
            with urllib.request.urlopen(request) as response:
                return response.status, ""
        except urllib.error.HTTPError as error:
            body = error.read().decode("utf-8", "replace").strip()
            if (error.code == 429 or error.code >= 500) and retries < 5:
                retries += 1
                delay = int(error.headers.get("Retry-After") or 0) or 2 ** retries
                print(f"  {error.code}, retrying in {delay}s", flush=True)
                time.sleep(delay)
                continue
            reset = QUOTA_RESET.search(body) if error.code == 403 else None
            if reset and quota_waits < MAX_QUOTA_WAITS:
                quota_waits += 1
                hours, minutes, seconds = (int(part) for part in reset.groups())
                delay = hours * 3600 + minutes * 60 + seconds + 30
                print(f"  quota exceeded, waiting {delay}s for it to be replenished", flush=True)
                time.sleep(delay)
                continue
            return error.code, f"{error.reason} {body}"[:500]


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
