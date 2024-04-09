#!/bin/bash
cd "$(dirname "$0")"

servicefolder="/var/lib/fiskaltrust"
if type jq > /dev/null;  then
  if [ -f launcher.configuration.json ]; then
    servicefolder=$(jq -r '.serviceFolder // "/var/lib/fiskaltrust"' launcher.configuration.json) || {
      echo "Failed to parse launcher.configuration.json"
      exit 1
    }
  else
    echo "launcher.configuration.json does not exist, using default service folder"
  fi
else
  echo "jq not found, using default service folder"
fi

# if default servicefolder does not exist, create it
if [ ! -d "$servicefolder" ]; then
  echo "Creating service folder $servicefolder"
  sudo mkdir "$servicefolder" || {
    echo "Failed to create service folder"
    exit 1
  }
  sudo chown -R $USER:$USER "$servicefolder" || {
    echo "Failed to change ownership of service folder"
    exit 1
  }
fi

# Uninstallation command
sudo ./fiskaltrust.Launcher uninstall

echo "Press any key to continue..."
read -n1 -s