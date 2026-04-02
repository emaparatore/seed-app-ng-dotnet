#!/bin/bash
# Entrypoint runs as root: fix bind-mount permissions, then drop to claude user.

# Fix permissions on bind-mounted .claude directory
# Windows NTFS bind mounts may have wrong ownership/permissions
if [ -d "/home/claude/.claude" ]; then
  chown -R claude:claude /home/claude/.claude 2>/dev/null
  chmod -R u+rwX /home/claude/.claude 2>/dev/null
fi

# Fix project directory ownership so claude can read/write all files and commit
if [ -d "/project" ]; then
  chown -R claude:claude /project 2>/dev/null
fi

# Fix execution permission on scripts (Windows NTFS mounts lose +x)
if [ -d "/project/scripts" ]; then
  chmod +x /project/scripts/*.sh 2>/dev/null
fi

# Drop privileges and exec as claude user
exec gosu claude "$@"
