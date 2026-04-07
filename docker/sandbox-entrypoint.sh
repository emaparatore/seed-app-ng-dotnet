#!/bin/bash
# Fix permissions on bind-mounted directories.
# Uses limited sudo (only chown/chmod allowed).

# Bootstrap isolated .claude config from host credentials (mounted read-only)
if [ -f "/tmp/claude-credentials.json" ]; then
  mkdir -p /home/claude/.claude
  cp /tmp/claude-credentials.json /home/claude/.claude/.credentials.json
  sudo chown -R claude:claude /home/claude/.claude 2>/dev/null
fi

# Fix project directory ownership so claude can read/write all files and commit
if [ -d "/project" ]; then
  sudo chown -R claude:claude /project 2>/dev/null
fi

# Fix execution permission on scripts (Windows NTFS mounts lose +x)
if [ -d "/project/scripts" ]; then
  sudo chmod +x /project/scripts/*.sh 2>/dev/null
fi

exec "$@"
