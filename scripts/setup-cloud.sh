#!/usr/bin/env bash
set -euo pipefail
cd /workspace/Test
source scripts/activate-cloud.sh
if [[ ! -x "$DOTNET_ROOT/dotnet" ]] || [[ "$(dotnet --version)" != "10.0.403" ]]; then
python3 - <<'PYSDK'
import hashlib, os, tarfile, tempfile, urllib.request
url = 'https://builds.dotnet.microsoft.com/dotnet/Sdk/10.0.403/dotnet-sdk-10.0.403-linux-x64.tar.gz'
expected = 'b7b3d8461cf98b07d2552ce9199702fa3ad010ac2671a5f8cd5136f7534e50f6855ba7bbb02b11494563f1d04156276a31cda1103b900e5eecd0d9473dc7b9c5'
with tempfile.TemporaryDirectory() as temp:
    archive = os.path.join(temp, 'sdk.tar.gz')
    urllib.request.urlretrieve(url, archive)
    with open(archive, 'rb') as data:
        actual = hashlib.file_digest(data, 'sha512').hexdigest()
    if actual.lower() != expected.lower():
        raise RuntimeError('SHA-512 del SDK incorrecto; se cancela la instalación')
    os.makedirs('/workspace/.dotnet', exist_ok=True)
    with tarfile.open(archive) as bundle:
        bundle.extractall('/workspace/.dotnet', filter='data')
PYSDK
fi
dotnet restore DesktopDrawers.sln
dotnet build DesktopDrawers.sln -c Release --no-restore
dotnet run --project tests/DesktopDrawers.Tests -c Release --no-build
