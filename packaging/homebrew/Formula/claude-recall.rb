# typed: false
# frozen_string_literal: true

class ClaudeRecall < Formula
  desc "AI-powered TUI to search Claude Code session history"
  homepage "https://github.com/GordonBeeming/claude-recall"
  version "VERSION_PLACEHOLDER"
  license "MIT"

  on_macos do
    if Hardware::CPU.arm?
      url "https://github.com/GordonBeeming/claude-recall/releases/download/TAG_PLACEHOLDER/claude-recall-osx-arm64.tar.gz"
      sha256 "SHA256_OSX_ARM64_PLACEHOLDER"
    else
      url "https://github.com/GordonBeeming/claude-recall/releases/download/TAG_PLACEHOLDER/claude-recall-osx-x64.tar.gz"
      sha256 "SHA256_OSX_X64_PLACEHOLDER"
    end
  end

  on_linux do
    if Hardware::CPU.arm?
      url "https://github.com/GordonBeeming/claude-recall/releases/download/TAG_PLACEHOLDER/claude-recall-linux-arm64.tar.gz"
      sha256 "SHA256_LINUX_ARM64_PLACEHOLDER"
    else
      url "https://github.com/GordonBeeming/claude-recall/releases/download/TAG_PLACEHOLDER/claude-recall-linux-x64.tar.gz"
      sha256 "SHA256_LINUX_X64_PLACEHOLDER"
    end
  end

  def install
    bin.install "claude-recall"
  end

  test do
    assert_match version.to_s, shell_output("#{bin}/claude-recall --version")
  end
end
