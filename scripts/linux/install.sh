#!/bin/bash
cd "$(dirname "$0")"

if type jq > /dev/null && [ -f launcher.configuration.json ]; then
  servicefolder=$(jq -r '.serviceFolder' launcher.configuration.json) || {
    echo "Failed to parse launcher.configuration.json"
    exit 1
  }
else
  echo "jq not found or launcher.configuration.json does not exist, using default service folder"
  servicefolder="/var/lib/fiskaltrust"
fi

# if default servicefolder does not exist, create it
if [ ! -d $servicefolder ]; then
  echo "Creating service folder"
  sudo mkdir $servicefolder || {
    echo "Failed to create service folder"
    exit 1
  }
  sudo chown -R $USER:$USER $servicefolder || {
    echo "Failed to change ownership of service folder"
    exit 1
  }
fi

# Installation command
sudo ./fiskaltrust.Launcher install

echo "Press any key to continue..."
read -n1 -s