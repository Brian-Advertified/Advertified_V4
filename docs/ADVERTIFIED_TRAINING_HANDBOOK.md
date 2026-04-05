# Advertified Training Handbook

**Version:** 1.0  
**Prepared For:** Internal training, onboarding, and handover  
**System:** Advertified  
**Document Type:** Operational handbook  

---

## 1. Introduction

### 1.1 Purpose

This handbook is designed to help teams understand how to use the Advertified system in day-to-day operations.

It can be used for:

- onboarding new staff
- refresher training
- handover between teams
- operational support reference

### 1.2 What This Handbook Covers

This handbook explains:

- the main user roles in Advertified
- the end-to-end campaign workflow
- the key screens used by each role
- standard operational actions
- common issues and how to troubleshoot them

### 1.3 Intended Audience

This document is suitable for:

- client success staff
- media planning and agent teams
- creative teams
- admin and operations teams
- managers overseeing campaign delivery

---

## 2. System Summary

Advertified is a campaign workflow system that guides a customer from package selection through campaign planning, recommendation approval, creative production, booking, and launch.

The platform is structured around four main workspaces:

- **Client workspace**: where customers submit briefs, review approvals, and follow campaign progress
- **Agent workspace**: where internal teams prepare recommendations, communicate with clients, and manage operational delivery
- **Creative workspace**: where creative directors generate and refine creative output
- **Admin workspace**: where administrators manage payments, users, outlets, pricing, rules, and platform settings

---

## 3. Core Workflow

The standard campaign journey follows the sequence below.

### 3.1 Step 1: Package Selection

The customer begins by selecting a package.

Typical path:

- `/packages`

Outcome:

- a package is selected
- pricing is confirmed
- the user moves toward checkout

### 3.2 Step 2: Payment and Campaign Creation

After payment is completed, the system creates or links the customer to a campaign.

Typical path:

- `/checkout/payment`
- `/checkout/confirmation`

Outcome:

- payment is recorded
- a campaign becomes visible in the system
- the client can move into the briefing stage

### 3.3 Step 3: Brief Submission

The client completes the campaign brief with business, audience, geography, and campaign direction details.

Typical path:

- `/campaigns/:id`

Outcome:

- the brief is saved
- the brief is submitted
- the campaign moves into planning

### 3.4 Step 4: Recommendation Planning

An agent reviews the brief and creates a recommendation.

Typical agent actions:

- review campaign detail
- initialize recommendation
- generate recommendation
- adjust proposal content
- send proposal to client

Outcome:

- the client receives proposal options
- the campaign moves into review

### 3.5 Step 5: Recommendation Approval

The client reviews the proposal and either approves or requests changes.

Typical path:

- `/campaigns/:id/approvals`

Outcome:

- the selected recommendation becomes the approved production route
- the campaign moves into creative production

### 3.6 Step 6: Creative Production

The creative director uses the approved recommendation to produce creative output.

Typical path:

- `/creative/campaigns/:id/studio`

Outcome:

- a creative system is produced
- final media is prepared
- finished media is sent back to the client for sign-off

### 3.7 Step 7: Booking and Launch

After creative approval, supplier booking and launch preparation continue.

Typical actions:

- save supplier bookings
- upload operational files
- record status updates
- mark campaign live

Outcome:

- the client sees campaign progress
- campaign reaches launch state

---

## 4. User Roles and Responsibilities

## 4.1 Client Role

### Main Areas Used

- `/dashboard`
- `/orders`
- `/campaigns/:id`
- `/campaigns/:id/approvals`
- `/campaigns/:id/messages`

### Main Responsibilities

- choose a package
- complete payment
- complete and submit the brief
- review proposal options
- approve or request changes
- review creative for final sign-off
- track booking and launch progress

### What Clients Should Understand

- the difference between a draft stage and an approval stage
- how to use messages versus approvals
- how to read status and next-step labels
- where to find current campaign actions

---

## 4.2 Agent Role

### Main Areas Used

- `/agent`
- `/agent/campaigns`
- `/agent/recommendations/new`
- `/agent/briefs`
- `/agent/review-send`
- `/agent/messages`
- `/agent/sales`

### Main Responsibilities

- monitor incoming campaigns
- review client briefs
- create recommendations
- send proposals to clients
- manage assignment state
- save bookings
- upload delivery and operational files
- update launch progress

### What Agents Should Understand

- how campaign statuses affect available actions
- when a recommendation can be sent
- how bookings affect what the client sees
- where client messaging is linked to campaign records

---

## 4.3 Creative Director Role

### Main Areas Used

- `/creative`
- `/creative/studio-demo`
- `/creative/campaigns/:id/studio`

### Main Responsibilities

- review approved campaign context
- generate creative systems
- iterate on creative direction
- upload creative files
- send finished media to the client
- support handoff into booking and launch prep

### What Creative Directors Should Understand

- creative work starts after recommendation approval
- the approved recommendation is the strategic source of truth
- file uploads should be correctly typed and clearly named
- sending finished media changes the client’s approval state

---

## 4.4 Admin Role

### Main Areas Used

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

### Main Responsibilities

- manage payment and order issues
- oversee campaign operations
- maintain user access
- manage catalog and pricing data
- maintain geography and rules
- monitor platform integrations
- maintain AI tools and voice catalog data
- review audit information

### What Admins Should Understand

- which changes affect live workflow behaviour
- how pricing and catalog data affect recommendations
- how refunds or payment changes affect campaign states
- where to look first during issue investigation

---

## 5. Workspace Guides

## 5.1 Client Workspace Guide

### Dashboard

Purpose:

- view all campaigns
- find the next active item
- open the relevant campaign

### Orders

Purpose:

- review order history
- confirm payment state
- cross-check package purchases

### Campaign Detail

Main tabs:

- Overview
- Approvals
- Messages

Client training focus:

- saving and submitting the brief
- understanding what is awaiting input
- reviewing approvals correctly
- using the message area for clarifications

---

## 5.2 Agent Workspace Guide

### Dashboard and Campaigns

Purpose:

- monitor queue health
- identify urgent campaigns
- open assigned or available campaigns

### Recommendation Creation

Purpose:

- turn a submitted brief into a proposal

Normal sequence:

1. open campaign
2. review brief
3. initialize recommendation
4. generate recommendation
5. review and refine
6. send to client

### Booking and Delivery

Purpose:

- update the campaign after approval
- reflect supplier progress
- mark launch milestones

---

## 5.3 Creative Workspace Guide

### Creative Dashboard

Purpose:

- review approved campaigns ready for studio work

### Creative Studio

The studio includes:

- campaign context
- production composition
- client handoff area
- creative system engine
- creative output review
- saved versions
- studio file upload area

Training focus:

- understanding the production brief
- generating a base creative system
- using iterations intentionally
- reviewing output before client handoff

---

## 5.4 Admin Workspace Guide

### Daily Work

Key areas:

- Payments
- Campaigns
- Users

### Catalog

Key areas:

- Outlets
- Pricing
- Imports
- Catalog health
- Geography

### Settings and Monitoring

Key areas:

- Planning rules
- Preview rules
- Integrations
- Monitoring
- Audit log

### AI Tools

Key areas:

- voice library
- voice packs
- voice templates
- ad operations

---

## 6. Key Status Meanings

These statuses are important during training because they determine what users can do next.

- `awaiting_purchase`: payment not yet completed
- `paid`: payment confirmed
- `brief_in_progress`: client still completing brief
- `brief_submitted`: brief ready for planning
- `planning_in_progress`: recommendation planning underway
- `review_ready`: proposal ready for client review
- `approved`: recommendation approved
- `creative_sent_to_client_for_approval`: finished media sent to client
- `creative_changes_requested`: client requested creative revisions
- `creative_approved`: finished creative approved
- `booking_in_progress`: supplier booking in progress
- `launched`: campaign is live

---

## 7. Training Delivery Plan

This handbook works best when delivered over structured sessions.

### Session 1: System Orientation

- introduce the platform
- explain workspaces and role boundaries
- review the end-to-end campaign flow

### Session 2: Client Journey

- package selection
- brief completion
- approvals
- messages

### Session 3: Agent Operations

- queue review
- recommendation creation
- proposal send
- booking updates

### Session 4: Creative Workflow

- studio navigation
- creative generation
- asset upload
- client handoff

### Session 5: Admin Operations

- payments
- campaigns
- users
- catalog management
- settings and monitoring

---

## 8. Troubleshooting Reference

### Issue: Client says payment is complete but payment is still requested

Check:

- package order payment status
- linked campaign payment state
- campaign workflow status

### Issue: Recommendation draft exists but workflow did not advance

Check:

- recommendation record status
- campaign status
- whether the stepper or review logic is reflecting the draft

### Issue: Creative team cannot proceed

Check:

- recommendation approval is complete
- campaign is visible in creative workflow
- required brief details are present

### Issue: Booking is not visible to the client

Check:

- booking was saved successfully
- correct campaign was updated
- campaign status has moved into booking

### Issue: Build or support environment behaves inconsistently

Check:

- stale running test or app processes
- locked build outputs
- recent dependency injection or startup changes

---

## 9. Best Practice Summary

### For Clients

- provide complete brief information
- use approvals for formal decisions
- use messages for discussion and clarification

### For Agents

- review the brief before generation
- confirm proposal quality before sending
- save operational updates promptly

### For Creative Directors

- work from approved strategy only
- review outputs carefully before client handoff
- keep uploaded file sets clean and traceable

### For Admins

- make controlled changes to catalog and rules
- verify payment and workflow effects before escalation
- use monitoring and audit tools for investigations

---

## 10. Handover Notes

This handbook should be updated whenever any of the following change:

- role responsibilities
- route structure
- campaign statuses
- recommendation approval flow
- creative handoff process
- admin workspace navigation
- AI workflow tooling

Recommended next step for handover:

- add screenshots for each workspace
- create one role-specific appendix per team
- export this file to PDF for formal distribution

---

## 11. Source Reference

This handbook was aligned to the current application structure, including:

- client routes
- agent routes
- creative routes
- admin routes
- current campaign workflow states

Primary companion reference:

- [`SYSTEM_TRAINING_MANUAL.md`](/c:/Users/CC%20KEMPTON/source/Advertified_V4/docs/SYSTEM_TRAINING_MANUAL.md)
