# Source this file from bash. Keep all caches inside the writable workspace.
export DOTNET_ROOT=/workspace/.dotnet
export PATH="$DOTNET_ROOT:$PATH"
export DOTNET_CLI_HOME=/workspace/.dotnet-cli
export NUGET_PACKAGES=/workspace/.nuget/packages
export XDG_RUNTIME_DIR=/workspace/.runtime
export XDG_DATA_HOME=/workspace/.local/share
export DOTNET_CLI_TELEMETRY_OPTOUT=1
export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=1
mkdir -p "$XDG_RUNTIME_DIR"
chmod 700 "$XDG_RUNTIME_DIR"
