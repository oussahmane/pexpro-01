#!/usr/bin/env bash
# Selects a .NET 10 SDK that includes Microsoft.AspNetCore.App.
# Arch's dotnet-sdk package does not include ASP.NET. Install:
#   sudo pacman -S --needed aspnet-runtime aspnet-targeting-pack

export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_NOLOGO=1

if ! command -v dotnet >/dev/null 2>&1 || ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App'; then
  if [[ -x "${HOME}/.dotnet/dotnet" ]] && "${HOME}/.dotnet/dotnet" --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App'; then
    export PATH="${HOME}/.dotnet:${PATH}"
    export DOTNET_ROOT="${HOME}/.dotnet"
  fi
fi

if ! dotnet --list-runtimes 2>/dev/null | grep -q 'Microsoft.AspNetCore.App'; then
  echo "The dotnet on PATH cannot build ASP.NET Core (Microsoft.AspNetCore.App is missing)." >&2
  echo "Install the matching Arch packages:" >&2
  echo "  sudo pacman -S --needed aspnet-runtime aspnet-targeting-pack" >&2
  return 1 2>/dev/null || exit 1
fi
