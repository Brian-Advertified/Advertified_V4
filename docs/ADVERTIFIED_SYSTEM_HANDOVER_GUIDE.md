# Advertified System Handover Guide

Version: April 2026  
Audience: Operations, sales, campaign, creative, support, and admin teams  
Document owner: Advertified internal team

## 1. Document Purpose

This handover guide is a polished training reference for teams using the Advertified platform in day-to-day work.

It is designed to help new and existing users:

- understand what the system does
- know which workspace belongs to which role
- follow the campaign lifecycle from purchase to launch
- navigate the most important screens quickly
- troubleshoot common operational issues

This document is based on the current application structure in the live codebase.

## 2. Platform Summary

Advertified is a campaign workflow platform that supports the full journey from package purchase through planning, approvals, creative production, booking, and reporting.

The platform is organized into four main role-based workspaces:

- Client workspace
- Agent workspace
- Creative workspace
- Admin workspace

Each workspace is purpose-built for a different part of the campaign lifecycle, but all of them operate around the same campaign record.

## 3. Core Business Flow

At a high level, the system supports the following flow:

1. A customer selects and pays for a package.
2. A campaign record is created.
3. The customer completes the brief.
4. An agent prepares recommendation options.
5. The customer approves or requests changes.
6. The creative team prepares media where required.
7. The operations or agent team completes bookings.
8. The campaign goes live.
9. Delivery reporting and follow-up are captured.

This flow can begin from either:

- a package-led purchase flow
- a recommendation-led campaign workflow

The intended business behavior is that both routes converge into the same downstream approval, production, booking, and reporting journey.

## 4. Role Overview

### 4.1 Client

The client uses the system to buy, brief, review, approve, and track campaigns.

Typical responsibilities:

- browse packages
- complete payment
- submit the campaign brief
- review proposal options
- approve or revise recommendations
- approve finished creative
- track campaign progress and messages

Typical routes:

- `/packages`
- `/checkout/payment`
- `/checkout/confirmation`
- `/dashboard`
- `/orders`
- `/campaigns/:id`
- `/campaigns/:id/approvals`
- `/campaigns/:id/messages`
- `/campaigns/:id/studio-preview`

### 4.2 Agent

The agent uses the system to plan campaigns, create recommendations, coordinate with clients, and manage operational progress.

Typical responsibilities:

- review campaign briefs
- create recommendation drafts
- refine and send proposals
- manage approvals follow-up
- coordinate bookings
- upload supporting files
- update operational and reporting details

Typical routes:

- `/agent`
- `/agent/leads`
- `/agent/briefs`
- `/agent/recommendation-builder`
- `/agent/review-send`
- `/agent/approvals`
- `/agent/messages`
- `/agent/sales`
- `/agent/recommendations/new`
- `/agent/campaigns`
- `/agent/campaigns/:id`

### 4.3 Creative Director

The creative team uses the platform after campaign direction has been approved and production work is ready to begin.

Typical responsibilities:

- review the approved campaign context
- generate or prepare creative direction
- upload and manage studio assets
- prepare final deliverables
- send outputs for client review

Typical routes:

- `/creative`
- `/creative/studio-demo`
- `/creative/campaigns/:id/studio`

### 4.4 Admin

The admin workspace is the system control center for finance, operations, catalog health, configuration, and platform oversight.

Typical responsibilities:

- monitor package orders and payments
- oversee campaign exceptions
- maintain user access
- manage outlet and pricing data
- manage imports and geography
- configure planning and preview rules
- review integrations, monitoring, and audit logs
- manage AI voice and ad operations tools

Typical routes:

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

## 5. Workspace Navigation Summary

### 5.1 Public and Entry Screens

These are the main pre-login or shared-access routes:

- `/`
- `/register`
- `/login`
- `/verify-email`
- `/set-password`
- `/packages`
- `/how-it-works`
- `/about`
- `/faq`
- `/media-partners`
- `/partner-enquiry`
- `/privacy`
- `/cookie-policy`
- `/terms-of-service`
- `/proposal/:id`

### 5.2 Admin Navigation Structure

The admin workspace is currently grouped into these sections:

- Overview
  Dashboard
- Daily work
  Payments, Campaigns, Users
- Catalog
  Outlets, Pricing, Imports, Catalog Health, Geography
- Settings
  Planning Rules, Preview Rules, Integrations, Monitoring, Audit Log
- AI tools
  Voice Library, Voice Packs, Voice Templates, Ad Operations

## 6. End-to-End Campaign Journey

### Stage 1: Package Purchase

The campaign journey usually starts when a client selects a package and completes payment.

Expected result:

- a package order is created
- a campaign record is created or linked
- the campaign becomes available to internal teams

### Stage 2: Client Brief

The client completes the campaign brief from the client workspace.

Expected result:

- brief information is saved
- planning can begin

### Stage 3: Recommendation Creation

The agent reviews the brief and prepares recommendation options.

Expected result:

- a recommendation draft exists
- proposal options can be refined and sent

### Stage 4: Client Approval

The client reviews the recommended options and chooses whether to approve or request revisions.

Expected result:

- an option is accepted, revised, or rejected
- the campaign moves into production or back into planning

### Stage 5: Creative Production

If creative work is needed, the creative workspace becomes active after the recommendation is accepted.

Expected result:

- the creative system is prepared
- assets are uploaded
- outputs are shared for review

### Stage 6: Booking and Delivery Preparation

The operations or agent team records bookings and prepares the campaign for launch.

Expected result:

- booking records are saved
- supporting files and proof documents are available
- readiness for launch is visible in the campaign record

### Stage 7: Live Campaign and Reporting

Once the campaign is live, the team tracks execution and delivery.

Expected result:

- status is updated
- reporting is attached
- post-launch visibility is available to the relevant teams

## 7. Role-Based Operating Guide

### 7.1 Client Operating Guide

The client should normally follow this path:

1. Select a package.
2. Complete checkout.
3. Open the campaign from the dashboard.
4. Submit the brief.
5. Review recommendations on the approvals screen.
6. Approve or request changes.
7. Review creative if presented.
8. Monitor messages and campaign progress.

### 7.2 Agent Operating Guide

The agent should normally follow this path:

1. Open the campaign queue.
2. Review the brief and campaign context.
3. Create or initialize the recommendation draft.
4. Build and refine recommendation options.
5. Send the proposal to the client.
6. Track approval status.
7. Progress the campaign into booking and delivery support.

### 7.3 Creative Operating Guide

The creative team should normally follow this path:

1. Open the studio for the approved campaign.
2. Review campaign and proposal context.
3. Prepare the creative system.
4. Upload or generate assets.
5. Finalize outputs.
6. Send creative for review and approval.

### 7.4 Admin Operating Guide

The admin team should normally use the workspace to:

- confirm payment and package order health
- investigate blocked campaigns
- check catalog and pricing data
- review integration or import problems
- monitor audit history
- maintain configuration and user access

## 8. Status and Decision Points

The system contains multiple status-driven experiences. Teams should pay attention to:

- payment cleared vs payment required
- brief drafted vs submitted
- recommendation drafted vs sent vs approved
- creative in progress vs awaiting approval
- booking in progress vs live

Important operational note:

Some campaigns begin with a direct package purchase and others progress through recommendation-first flows. Users should be trained to verify the campaign state itself rather than relying only on assumptions about how the campaign started.

## 9. Common Operational Scenarios

### Scenario 1: Client has paid but still sees payment messaging

Check:

- whether the campaign reflects the correct payment-cleared state
- whether the linked package order is marked correctly
- whether the user is viewing the right campaign and proposal

### Scenario 2: Agent creates a recommendation draft but the workflow step does not advance

Check:

- whether a recommendation draft record exists
- whether the campaign status still reflects planning
- whether the UI stepper is using recommendation state as well as campaign state

### Scenario 3: Creative team cannot proceed

Check:

- whether the recommendation has been approved
- whether the campaign is ready for production
- whether the correct campaign studio route is being used

### Scenario 4: Admin sees a campaign operational mismatch

Check:

- package order state
- campaign status
- recommendation status
- payment linkage
- latest audit or integration activity

## 10. Training Plan for New Users

Recommended onboarding format:

### Day 1

- system overview
- user account access
- workspace orientation
- route walkthrough by role

### Day 2

- package-to-campaign workflow demo
- brief submission demo
- recommendation approval demo

### Day 3

- creative and booking workflow demo
- admin controls and exception handling
- troubleshooting exercises

### Day 4

- supervised hands-on usage
- role-specific practice
- issue logging and escalation practice

## 11. Suggested Handover Checklist

Before a team is signed off on training, confirm that they can:

- explain the end-to-end campaign flow
- identify the correct workspace for their role
- locate their most-used screens
- understand the approval chain
- identify when payment, recommendation, and creative stages are complete
- escalate a blocked campaign with the right context

## 12. Support and Escalation

When logging a system issue, capture the following:

- campaign ID
- package order ID if available
- user role affected
- exact screen or route
- what the user expected
- what actually happened
- screenshots if possible

This makes it much faster for product, engineering, or operations teams to trace the problem.

## 13. Glossary

- Campaign: The main record tracking a customer advertising initiative.
- Package order: The commercial order that usually starts the journey.
- Brief: The customer-submitted planning input.
- Recommendation: The proposed media plan or option set prepared by an agent.
- Approval: A decision point where the client accepts, revises, or rejects work.
- Creative studio: The workspace used for creative preparation and output management.
- Booking: The process of confirming media placement or delivery execution.

## 14. Document Notes

This version is prepared as a handover-style Markdown document so it can be kept in the repository, reviewed in the browser, or exported to PDF from an editor such as VS Code.

If needed, a next step can be to produce:

- a branded PDF version for external handover
- role-specific quick-reference manuals
- slide-based training packs for workshops
