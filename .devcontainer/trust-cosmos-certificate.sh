#!/usr/bin/env bash
set -euo pipefail

curl --fail --silent --show-error --retry 60 --retry-delay 2 --retry-all-errors \
  --max-time 5 --retry-max-time 180 http://localhost:8080/ready > /dev/null

certificate=$(mktemp)
trap 'rm -f "$certificate"' EXIT
openssl s_client -connect localhost:8081 -servername localhost </dev/null 2>/dev/null \
  | openssl x509 -outform PEM > "$certificate"
sudo -n install -m 0644 "$certificate" /usr/local/share/ca-certificates/cosmos-emulator.crt
sudo -n update-ca-certificates