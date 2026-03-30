#!/usr/bin/env bash
set -euo pipefail

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
PROJECT="$SCRIPT_DIR/src/ClaudeRecall/ClaudeRecall.csproj"
INSTALL_DIR="$HOME/.local/bin"

# Detect OS
case "$(uname -s)" in
    Darwin) OS="osx" ;;
    Linux)  OS="linux" ;;
    *)      echo "Unsupported OS: $(uname -s)"; exit 1 ;;
esac

# Detect arch
case "$(uname -m)" in
    arm64|aarch64) ARCH="arm64" ;;
    x86_64)        ARCH="x64" ;;
    *)             echo "Unsupported arch: $(uname -m)"; exit 1 ;;
esac

RID="${OS}-${ARCH}"
VERSION=$(cat "$SCRIPT_DIR/VERSION" | tr -d '[:space:]')

echo "Building claude-recall ${VERSION} for ${RID}..."

dotnet publish "$PROJECT" \
    -c Release \
    -r "$RID" \
    -p:ClaudeRecallVersion="$VERSION" \
    -o "$SCRIPT_DIR/publish/$RID"

BINARY="$SCRIPT_DIR/publish/$RID/claude-recall"

if [ ! -f "$BINARY" ]; then
    echo "Build failed — binary not found at $BINARY"
    exit 1
fi

echo ""
echo "Binary: $BINARY"
echo "Size: $(du -h "$BINARY" | cut -f1)"
echo ""

# Install to ~/.local/bin
mkdir -p "$INSTALL_DIR"
cp "$BINARY" "$INSTALL_DIR/claude-recall"
chmod +x "$INSTALL_DIR/claude-recall"

echo "Installed to $INSTALL_DIR/claude-recall"
echo ""
"$INSTALL_DIR/claude-recall" --version
