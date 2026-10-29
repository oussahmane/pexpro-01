#!/usr/bin/env bash
# This project uses the system PostgreSQL service, which also serves other
# databases on this machine. Stopping it from here would take those offline.
echo "Pexilio does not stop the system PostgreSQL service."
echo "pexilio_dev and pexilio_test stay in place. Other databases stay in place."
echo "To stop PostgreSQL yourself: sudo systemctl stop postgresql"
