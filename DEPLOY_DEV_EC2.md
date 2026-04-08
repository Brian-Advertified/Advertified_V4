# DEV EC2 Git-Based Deploy

This repo now supports **git-driven DEV deployment** via GitHub Actions.

## What this does

Workflow file: `.github/workflows/deploy-dev-ec2.yml`

It deploys to DEV by:
1. SSH into EC2 DEV
2. `git pull --ff-only origin main`
3. Rebuild + restart `api` and `web` containers only

No PROD commands are included.

## One-time GitHub setup

Add these **Repository Secrets**:

- `DEV_EC2_HOST` (example: `13.246.60.13`)
- `DEV_EC2_USER` (example: `ubuntu`)
- `DEV_EC2_SSH_PRIVATE_KEY` (full private key text)
- `DEV_EC2_PORT` (optional, defaults to `22`)

Optional **Repository Variables** (if your paths differ):

- `DEV_APP_DIR` (default: `$HOME/apps/advertified-v4-dev`)
- `DEV_COMPOSE_FILE` (default: `compose.v4-dev.yml`)
- `DEV_ENV_FILE` (default: `.deploy-v4-dev/v4-dev.env`)

## How to deploy

Option 1:
- Push to `main` (changes under `src/**` or `tests/**`)

Option 2:
- Run workflow manually from **Actions -> Deploy DEV to EC2 -> Run workflow**

## Notes

- Keep DEV and PROD on separate compose files/env files and hosts.
- This workflow intentionally targets DEV only.
