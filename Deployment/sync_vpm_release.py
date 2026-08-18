#!/usr/bin/env python3
"""Mirror the latest GitHub Release into a static VPM repository."""

from __future__ import annotations

import argparse
import hashlib
import json
import os
import re
import sys
import tempfile
import urllib.error
import urllib.request
from pathlib import Path


DEFAULT_GITHUB_REPOSITORY = "00b7ce/Blendshape-Animation-Creator"
EXPECTED_PACKAGE_NAME = "com.mekabu.blendshape-animation-creator"
DEFAULT_REPOSITORY_ROOT = "/mnt/data/projects/vpm-repository"
DEFAULT_PUBLIC_BASE_URL = "https://vpm-repo.mekabu.io"
USER_AGENT = "Mekabu-VPM-Repository-Sync/1.0"


def request_bytes(url: str) -> bytes:
    headers = {
        "Accept": "application/vnd.github+json",
        "User-Agent": USER_AGENT,
        "X-GitHub-Api-Version": "2022-11-28",
    }
    token = os.environ.get("GITHUB_TOKEN")
    if token:
        headers["Authorization"] = f"Bearer {token}"
    request = urllib.request.Request(url, headers=headers)
    with urllib.request.urlopen(request, timeout=30) as response:
        return response.read()


def request_json(url: str) -> dict:
    return json.loads(request_bytes(url).decode("utf-8"))


def sha256(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for block in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(block)
    return digest.hexdigest()


def write_json_atomic(path: Path, value: dict) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{path.name}.", suffix=".tmp", dir=str(path.parent)
    )
    try:
        with os.fdopen(fd, "w", encoding="utf-8", newline="\n") as stream:
            json.dump(value, stream, ensure_ascii=False, indent=2)
            stream.write("\n")
            stream.flush()
            os.fchmod(stream.fileno(), 0o664)
        os.replace(temporary_name, path)
    except BaseException:
        try:
            os.unlink(temporary_name)
        except FileNotFoundError:
            pass
        raise


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--root", default=DEFAULT_REPOSITORY_ROOT)
    parser.add_argument("--github-repository", default=DEFAULT_GITHUB_REPOSITORY)
    parser.add_argument("--public-base-url", default=DEFAULT_PUBLIC_BASE_URL)
    parser.add_argument("--tag", help="Release tag to mirror; defaults to latest")
    args = parser.parse_args()

    root = Path(args.root).resolve()
    index_path = root / "index.json"
    if not index_path.is_file():
        raise RuntimeError(f"Repository index does not exist: {index_path}")

    endpoint = (
        f"https://api.github.com/repos/{args.github_repository}/releases/tags/{args.tag}"
        if args.tag
        else f"https://api.github.com/repos/{args.github_repository}/releases/latest"
    )
    release = request_json(endpoint)
    assets = {asset["name"]: asset for asset in release.get("assets", [])}

    manifest_asset = assets.get("package.json")
    if manifest_asset is None:
        raise RuntimeError("The GitHub Release does not contain package.json")

    manifest = json.loads(
        request_bytes(manifest_asset["browser_download_url"]).decode("utf-8")
    )
    package_name = manifest.get("name")
    version = manifest.get("version")
    author = manifest.get("author", {})
    if package_name != EXPECTED_PACKAGE_NAME:
        raise RuntimeError(f"Unexpected package name: {package_name!r}")
    if not isinstance(version, str) or not re.fullmatch(
        r"(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)\.(?:0|[1-9]\d*)(?:-[0-9A-Za-z.-]+)?",
        version,
    ):
        raise RuntimeError(f"Unsafe or invalid package version: {version!r}")
    if not isinstance(author, dict):
        raise RuntimeError("Package manifest author must be an object")
    required_values = {
        "name": package_name,
        "displayName": manifest.get("displayName"),
        "version": version,
        "author.name": author.get("name"),
        "author.email": author.get("email"),
    }
    missing = [name for name, value in required_values.items() if not value]
    if missing:
        raise RuntimeError(f"Package manifest is missing: {', '.join(missing)}")

    zip_name = f"{package_name}-{version}.zip"
    zip_asset = assets.get(zip_name)
    if zip_asset is None:
        raise RuntimeError(f"The GitHub Release does not contain {zip_name}")

    destination_directory = root / "packages" / package_name / version
    destination_directory.mkdir(parents=True, exist_ok=True)
    destination_zip = destination_directory / zip_name

    fd, temporary_name = tempfile.mkstemp(
        prefix=f".{zip_name}.", suffix=".tmp", dir=str(destination_directory)
    )
    os.close(fd)
    temporary_zip = Path(temporary_name)
    try:
        with temporary_zip.open("wb") as stream:
            stream.write(request_bytes(zip_asset["browser_download_url"]))
        temporary_zip.chmod(0o664)
        downloaded_hash = sha256(temporary_zip)

        if destination_zip.exists():
            existing_hash = sha256(destination_zip)
            if existing_hash != downloaded_hash:
                raise RuntimeError(
                    f"Refusing to overwrite immutable release {package_name} {version}"
                )
            temporary_zip.unlink()
            destination_zip.chmod(0o664)
        else:
            os.replace(temporary_zip, destination_zip)
    finally:
        if temporary_zip.exists():
            temporary_zip.unlink()

    index = json.loads(index_path.read_text(encoding="utf-8"))
    index["author"] = "contact@mekabu.io"
    packages = index.setdefault("packages", {})
    versions = packages.setdefault(package_name, {}).setdefault("versions", {})

    public_url = (
        f"{args.public_base_url.rstrip('/')}/packages/"
        f"{package_name}/{version}/{zip_name}"
    )
    listing_manifest = dict(manifest)
    listing_manifest["url"] = public_url
    listing_manifest["zipSHA256"] = downloaded_hash
    versions[version] = listing_manifest
    write_json_atomic(index_path, index)

    print(f"Published {package_name} {version}")
    print(public_url)
    print(f"SHA-256: {downloaded_hash}")
    return 0


if __name__ == "__main__":
    try:
        raise SystemExit(main())
    except (RuntimeError, urllib.error.URLError, json.JSONDecodeError) as error:
        print(f"Error: {error}", file=sys.stderr)
        raise SystemExit(1)
