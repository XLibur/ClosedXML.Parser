#!/usr/bin/env bash
# Regenerates the visualizer stylesheet (src/ClosedXML.Parser.Visualizer/wwwroot/css/app.css)
# from its Tailwind input (Styles/app.css). The stylesheet is committed, so a normal build needs
# no Tailwind. The script downloads the pinned Tailwind standalone CLI into .tools/ once, and
# verifies it against the SHA-256 below.
#
#   tools/tailwind/generate-css.sh           write app.css
#   tools/tailwind/generate-css.sh --watch   write app.css, and again after each change
#   tools/tailwind/generate-css.sh --check   change nothing, fail if the committed app.css differs
#
# Line ends are not compared.
set -euo pipefail

# To update Tailwind, change the version and take the hashes from sha256sums.txt of the release.
version=4.3.3

mode=write
case "${1:-}" in
    "") ;;
    --check) mode=check ;;
    --watch) mode=watch ;;
    *) echo "usage: $0 [--check|--watch]" >&2; exit 2 ;;
esac

case "$(uname -s)-$(uname -m)" in
    Linux-x86_64) asset=tailwindcss-linux-x64; sha=dc61b3ac6b8c9ca874c0cc4c57b2409791a64c5540404ca5f5367360babc313a ;;
    Linux-aarch64 | Linux-arm64) asset=tailwindcss-linux-arm64; sha=55fd0b241214eff3de1e8ee4f22796662f2d2e7a49bcfca7477cfd0bac398195 ;;
    Darwin-arm64) asset=tailwindcss-macos-arm64; sha=cdf646702987a743464dff4d9c60fd4480d1c1e73dd819a9a67f1078815dce9d ;;
    Darwin-x86_64) asset=tailwindcss-macos-x64; sha=7922e0953f2110c05976e3bf58f14e643d90427575e766b7d433f5f80cbee7e1 ;;
    MINGW*-x86_64 | MSYS*-x86_64 | CYGWIN*-x86_64) asset=tailwindcss-windows-x64.exe; sha=e0e260ce048014e9268f6237ff18f8ccf02cef521cbd0ae04e82c2cdf7aa3955 ;;
    *) echo "Tailwind $version has no standalone CLI for $(uname -s) $(uname -m)." >&2; exit 1 ;;
esac

root=$(cd "$(dirname "$0")/../.." && pwd)
app="$root/src/ClosedXML.Parser.Visualizer"
input="$app/Styles/app.css"
output="$app/wwwroot/css/app.css"
cli="$root/.tools/tailwindcss/$version/$asset"

sha256() {
    if command -v sha256sum > /dev/null; then
        sha256sum "$1" | cut -d ' ' -f 1
    else
        shasum -a 256 "$1" | cut -d ' ' -f 1
    fi
}

if [ ! -x "$cli" ]; then
    mkdir -p "$(dirname "$cli")"
    echo "downloading Tailwind CSS $version ($asset)"
    curl -fsSL -o "$cli.download" "https://github.com/tailwindlabs/tailwindcss/releases/download/v$version/$asset"
    actual=$(sha256 "$cli.download")
    if [ "$actual" != "$sha" ]; then
        rm -f "$cli.download"
        echo "The SHA-256 of the downloaded $asset is $actual, but $sha is expected." >&2
        exit 1
    fi
    chmod +x "$cli.download"
    mv "$cli.download" "$cli"
fi

case "$mode" in
    write)
        "$cli" --input "$input" --output "$output"
        ;;
    watch)
        exec "$cli" --input "$input" --output "$output" --watch
        ;;
    check)
        work=$(mktemp -d)
        trap 'rm -rf "$work"' EXIT
        "$cli" --input "$input" --output "$work/app.css" 2> /dev/null
        if cmp -s <(tr -d '\r' < "$work/app.css") <(tr -d '\r' < "$output"); then
            echo "app.css is generated from Styles/app.css"
        else
            echo "app.css is not generated from Styles/app.css and the .razor files. Run tools/tailwind/generate-css.sh and commit app.css." >&2
            diff <(tr -d '\r' < "$output") <(tr -d '\r' < "$work/app.css") | head -n 20 >&2 || true
            exit 1
        fi
        ;;
esac
