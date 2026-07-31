#!/usr/bin/env bash
set -euo pipefail

if [[ $# -lt 2 ]]; then
  echo "Usage: $0 <exe-path> <private-key-pem-path>" >&2
  exit 1
fi

exe_path="$1"
private_key_path="$2"
script_dir="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

dotnet run --project "${script_dir}/SignUpdate/SignUpdate.csproj" -c Release -- "$exe_path" "$private_key_path"
