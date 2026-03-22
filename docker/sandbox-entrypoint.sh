#!/bin/bash
# Fix permissions on bind-mounted .claude directory
# Windows NTFS bind mounts may have wrong ownership/permissions
if [ -d "/home/claude/.claude" ]; then
  sudo chown -R claude:claude /home/claude/.claude 2>/dev/null
  sudo chmod -R u+rwX /home/claude/.claude 2>/dev/null
fi

# Fix execution permission on scripts (Windows NTFS mounts lose +x)
if [ -d "/project/scripts" ]; then
  sudo chmod +x /project/scripts/*.sh 2>/dev/null
fi

exec "$@"
