#!/usr/bin/env bash
set -euo pipefail

# Generates the self-signed certificate Kestrel serves HTTPS with in non-production
# environments. Equivalent to the New-SelfSignedCertificate + Export-PfxCertificate pair
# used on Windows.
#
# Usage: ./cert_renew.sh <servername> [password]

if [ -z "${1:-}" ]; then
    echo "Usage: $0 <servername> [password]" >&2
    exit 1
fi

servername="$1"
password="${2:-password}"

certs_dir="./certs"
mkdir -p "$certs_dir"

key_path="$certs_dir/certificate.key"
crt_path="$certs_dir/certificate.crt"
pfx_path="$certs_dir/certificate.pfx"

openssl req -x509 \
    -newkey rsa:2048 \
    -keyout "$key_path" \
    -out "$crt_path" \
    -days 365 \
    -nodes \
    -subj "/CN=${servername}" \
    -addext "subjectAltName=DNS:${servername}"

openssl pkcs12 -export \
    -out "$pfx_path" \
    -inkey "$key_path" \
    -in "$crt_path" \
    -passout pass:"$password"

rm -f "$key_path" "$crt_path"

echo "Certificate exported to $pfx_path"
