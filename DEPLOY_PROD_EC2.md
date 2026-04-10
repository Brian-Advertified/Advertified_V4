# PROD EC2 Git-Based Deploy

This repo supports **git-driven PROD deployment** via GitHub Actions.

Workflow file: `.github/workflows/deploy-prod-ec2.yml`

This workflow is intentionally **manual-only** (`workflow_dispatch`) to avoid accidental production deploys.

## One-time GitHub setup

Add these **Repository Secrets**:

- `PROD_EC2_HOST` (your PROD EC2 public IP / DNS)
- `PROD_EC2_USER` (example: `ubuntu`)
- `PROD_EC2_SSH_PRIVATE_KEY` (full private key text)
- `PROD_EC2_PORT` (optional; defaults to SSH action default if omitted)

Optional **Repository Variables** (if your paths differ):

- `PROD_APP_DIR` (default: `$HOME/apps/advertified-v4-prod`)
- `PROD_COMPOSE_FILE` (default: `compose.v4-prod.yml`)
- `PROD_ENV_FILE` (default: `.deploy-v4-prod/v4-prod.env`)

## Server prerequisites (on PROD EC2)

1. Ensure the app repo exists at `PROD_APP_DIR` and has the correct `origin` remote.
2. Ensure Docker + Docker Compose plugin are installed.
3. Ensure the compose file and env file exist on the server:
   - `compose.v4-prod.yml`
   - `.deploy-v4-prod/v4-prod.env`

## How to deploy

Run: **Actions -> Deploy PROD to EC2 -> Run workflow**

The deploy does:
1. SSH into EC2 PROD
2. `git pull --ff-only origin main`
3. `docker compose build api web`
4. `docker compose up -d api web`

## Notes

- Keep DEV and PROD on separate compose/env files and separate hosts.
- No database migration step is included; use your runbook before deploying if schema changes are required.

