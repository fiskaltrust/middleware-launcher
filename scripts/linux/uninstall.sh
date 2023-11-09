#!/bin/bash

# Checking for root privileges
if [ "$EUID" -ne 0 ]; then
  echo "Restarting with root privileges..."
  sudo "$0" "$@"
  exit $?
fi

# Running with root privileges
echo "Running with root privileges."

# Installation command
./fiskaltrust.Launcher uninstall
