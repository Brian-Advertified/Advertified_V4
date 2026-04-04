# Advertified System Training Manual

## Purpose

This manual is a practical guide for people using the Advertified system day to day. It explains:

- what each user role does
- where to find key screens
- the normal workflow from package purchase to campaign launch
- common actions in the client, agent, creative, and admin workspaces
- basic troubleshooting steps

This guide is based on the current system structure in the application.

## System Overview

Advertified is a campaign workflow platform with four main role-based workspaces:

- Client workspace: used by customers to buy packages, submit briefs, review recommendations, approve creative, and track campaign progress
- Agent workspace: used by internal agents to manage campaign planning, create recommendations, send proposals, manage bookings, and report delivery
- Creative workspace: used by creative directors to produce creative systems, upload assets, send finished media for approval, and support launch preparation
- Admin workspace: used by administrators to manage payments, campaign operations, users, catalog data, planning rules, AI tools, and platform settings

## User Roles

### Client

Clients normally use:

- `/packages`
- `/dashboard`
- `/orders`
- `/campaigns/:id`
- `/campaigns/:id/approvals`
- `/campaigns/:id/messages`

Main responsibilities:

- choose and pay for a package
- complete the campaign brief
- review and approve or request changes to recommendations
- approve finished creative or request creative changes
- follow campaign progress after booking and launch

### Agent

Agents normally use:

- `/agent`
- `/agent/campaigns`
- `/agent/recommendations/new`
- `/agent/briefs`
- `/agent/review-send`
- `/agent/approvals`
- `/agent/messages`
- `/agent/sales`

Main responsibilities:

- monitor campaign queue and assignments
- interpret briefs
- initialize and generate recommendations
- review, edit, and send recommendations to clients
- handle supplier bookings
- upload operational files
- log delivery updates and mark campaigns live

### Creative Director

Creative directors normally use:

- `/creative`
- `/creative/studio-demo`
- `/creative/campaigns/:id/studio`

Main responsibilities:

- work on campaigns after recommendation approval
- generate creative systems from approved campaign context
- iterate on creative direction
- upload creative files and final media
- send finished media to the client for final approval
- support booking and launch prep where needed

### Admin

Admins normally use:

- `/admin`
- `/admin/package-orders`
- `/admin/campaign-operations`
- `/admin/users`
- `/admin/stations`
- `/admin/pricing`
- `/admin/imports`
- `/admin/health`
- `/admin/geography`
- `/admin/engine`
- `/admin/preview-rules`
- `/admin/integrations`
- `/admin/monitoring`
- `/admin/audit`
- `/admin/ai-voices`
- `/admin/ai-voice-packs`
- `/admin/ai-voice-templates`
- `/admin/ai-ad-ops`

Main responsibilities:

- monitor payments and package orders
- manage campaign exceptions and refunds
- maintain users and permissions
- manage outlet, pricing, and geography data
- update planning and preview rules
- monitor integrations and AI operations
- review audit history

## End-to-End Workflow

### 1. Package Selection and Checkout

The client starts by selecting a package on `/packages`.

Typical steps:

- choose package band
- review pricing summary
- go to payment selection
- complete payment
- land on checkout confirmation

Expected outcome:

- a package order is created
- a linked campaign is created
- the campaign becomes available in the client dashboard and internal workspaces

### 2. Brief Submission

The client completes the campaign brief from the campaign workspace.

Typical brief topics:

- objective
- geography
- audience
- preferred media
- creative readiness
- special requirements

Expected outcome:

- the brief is saved
- the brief is submitted
- the campaign moves into planning

### 3. Recommendation Planning

The agent reviews the campaign in the agent workspace and creates a recommendation draft.

Typical agent steps:

- open assigned or available campaign
- review client brief
- initialize recommendation
- generate recommendation
- adjust proposal if needed
- send proposal to client

Expected outcome:

- the campaign moves into client review
- the client can see proposal options and approval actions

### 4. Recommendation Approval

The client reviews the proposal on the approvals screen.

Typical client decisions:

- approve selected proposal
- request changes
- continue to payment if required by the workflow

Expected outcome:

- approved recommendation becomes the production reference
- the campaign moves toward creative production

### 5. Creative Production

The creative director works in the creative studio after recommendation approval.

Typical creative steps:

- review approved campaign context
- generate creative system
- iterate on direction
- upload studio files
- prepare finished media
- send finished media to client

Expected outcome:

- client receives creative for final approval
- campaign moves to creative approval or booking stage

### 6. Booking and Launch

After creative approval, the operations and agent flow continues.

Typical actions:

- save supplier or station bookings
- upload supporting assets
- update booking status
- mark campaign live
- add delivery reporting

Expected outcome:

- client sees booking progress
- campaign transitions to launched status

## Client Training Guide

### Client Dashboard

Path: `/dashboard`

Use this page to:

- view all campaigns
- find active or pending work
- open a specific campaign
- move to packages if no campaign exists

### Orders

Path: `/orders`

Use this page to:

- review package order history
- confirm payment status
- check linked order information

### Campaign Workspace

Path: `/campaigns/:id`

Key sections:

- Overview
- Approvals
- Messages

What the client should learn:

- how to submit or revise a brief
- how to review recommendation options
- how to approve or request changes
- how to follow creative and booking progress
- how to message the internal team

### Client Best Practices

- complete the brief as clearly as possible before submission
- use approvals for formal decisions
- use messages for clarifications and context
- review status labels and next steps before escalating issues

## Agent Training Guide

### Agent Dashboard

Path: `/agent`

Use this page to:

- review personal queue
- identify urgent work
- open campaign detail pages

### Campaign Management

Path: `/agent/campaigns`

Agents should learn how to:

- open a campaign
- review status and timeline
- understand whether a campaign is unassigned, assigned, awaiting action, or waiting on client input

### Recommendation Creation

Path: `/agent/recommendations/new`

Typical process:

- load campaign context
- create recommendation draft
- review package fit and media logic
- save or regenerate as needed

### Review and Send

Path: `/agent/review-send`

Use this area to:

- confirm the recommendation is client-ready
- send the selected proposal to the client

### Messages

Path: `/agent/messages`

Use this area to:

- view campaign conversations
- respond to client questions
- keep communication tied to the correct campaign

### Sales

Path: `/agent/sales`

Use this area to:

- track prospect conversion activity
- review campaign sales progress

### Agent Best Practices

- always confirm the brief is complete before generating a recommendation
- check campaign status before sending or editing a recommendation
- keep proposal notes consistent with what the client will see
- log bookings promptly once supplier confirmation is received

## Creative Director Training Guide

### Creative Workspace

Paths:

- `/creative`
- `/creative/campaigns/:id/studio`

The creative workspace begins after recommendation approval.

### Creative Studio

The studio supports:

- creative briefing context
- production composition review
- creative system generation
- iteration passes
- AI job queue tracking
- regeneration using feedback
- saved creative versions
- file upload and studio asset management

### Core Creative Actions

- review creative direction and production notes
- generate a base creative system
- use iteration passes such as shorter, bolder, more premium, more Gen Z, or more performance
- review output including summary, master idea, storyboard, channel adaptations, visual direction, audio notes, and production notes
- upload creative pack, brand assets, or final media
- send finished media to the client when ready

### Creative Best Practices

- do not start production before recommendation approval is confirmed
- use the approved recommendation as the strategic anchor
- save and review iterations before sending final media
- make sure uploaded files are clearly named and correctly typed

## Admin Training Guide

### Admin Dashboard

Path: `/admin`

Use this page for:

- high-level operational visibility
- quick links into daily tasks

### Daily Work

Primary pages:

- `/admin/package-orders`
- `/admin/campaign-operations`
- `/admin/users`

Use these pages to:

- review payment issues
- update campaign operational state
- process exceptions such as refunds
- manage user accounts and access

### Catalog Management

Primary pages:

- `/admin/stations`
- `/admin/pricing`
- `/admin/imports`
- `/admin/health`
- `/admin/geography`

Use these pages to:

- maintain outlets and pricing
- import catalog updates
- review data health
- manage geography mappings

### Settings and Monitoring

Primary pages:

- `/admin/engine`
- `/admin/preview-rules`
- `/admin/integrations`
- `/admin/monitoring`
- `/admin/audit`

Use these pages to:

- manage planning rules
- review preview behaviour
- monitor integrations
- review audit entries

### AI Tools

Primary pages:

- `/admin/ai-voices`
- `/admin/ai-voice-packs`
- `/admin/ai-voice-templates`
- `/admin/ai-ad-ops`

Use these pages to:

- manage voice catalog content
- manage AI voice packs and templates
- review AI ad operations tooling

### Admin Best Practices

- make changes in a controlled order
- verify pricing and geography changes before publishing operational decisions
- use audit and monitoring views when investigating unexpected behaviour
- keep user-role assignments minimal and appropriate

## Common Status Meanings

Examples of campaign statuses used across the system:

- `awaiting_purchase`: campaign is linked to a package flow but payment is not complete
- `paid`: payment is confirmed and the campaign can proceed
- `brief_in_progress`: client is still completing the brief
- `brief_submitted`: brief has been submitted and planning can begin
- `planning_in_progress`: recommendation planning is underway
- `review_ready`: recommendation is ready for client review
- `approved`: recommendation is approved
- `creative_sent_to_client_for_approval`: finished creative is with the client
- `creative_changes_requested`: client requested creative changes
- `creative_approved`: client approved finished creative
- `booking_in_progress`: supplier booking and launch preparation is underway
- `launched`: campaign is live

## Troubleshooting Guide

### A client says payment is complete but the system still asks for payment

Check:

- package order payment status in admin package orders
- linked campaign payment status
- whether the campaign is in the correct post-payment status

### A recommendation draft was created but the campaign did not advance

Check:

- campaign status
- whether a draft recommendation exists
- whether the stepper or review state is reflecting the latest recommendation status

### Creative cannot proceed

Check:

- recommendation approval is complete
- campaign is accessible in the creative inbox
- required assets or creative notes are available

### Booking is not visible to the client

Check:

- supplier booking was actually saved
- campaign moved into booking state
- booking details were entered against the correct campaign

### Build or environment issue during support

Check:

- whether another local process is locking build outputs
- whether background test runners are still active
- whether startup dependency registrations were recently changed

## Suggested Training Plan

### Session 1: System Orientation

- explain roles and workspaces
- review navigation and terminology
- show the end-to-end workflow

### Session 2: Client Workflow

- package selection
- brief completion
- approvals
- messaging

### Session 3: Agent Workflow

- campaign queue
- recommendation generation
- send to client
- booking and reporting

### Session 4: Creative Workflow

- creative studio navigation
- creative system generation
- upload and approval handoff

### Session 5: Admin Workflow

- payments
- campaigns
- users
- catalog maintenance
- settings and audit

## Glossary

- Campaign: the main client project record moving through the workflow
- Brief: the client’s campaign requirements and targeting details
- Recommendation: the proposed media plan prepared for the client
- Creative system: the structured creative output used for production
- Booking: supplier or station confirmation for campaign execution
- Delivery report: an operational record showing delivery or performance evidence

## Maintenance Note

This document is a training baseline. It should be updated when any of the following change:

- route structure
- role responsibilities
- campaign statuses
- approval flow
- admin navigation
- AI tool workflows
