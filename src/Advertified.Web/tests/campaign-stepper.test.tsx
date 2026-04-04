import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CampaignStepper } from '../src/components/campaign/CampaignStepper';
import type { Campaign } from '../src/types/domain';

// Mock campaign data for different statuses
const createMockCampaign = (status: Campaign['status']): Campaign => ({
  id: 'test-campaign',
  userId: 'test-user',
  clientName: 'Test Client',
  clientEmail: 'client@test.com',
  businessName: 'Test Business',
  packageOrderId: 'test-order',
  packageBandId: 'test-package',
  packageBandName: 'Test Package',
  selectedBudget: 50000,
  paymentStatus: 'paid',
  status,
  planningMode: 'agent_assisted',
  aiUnlocked: false,
  agentAssistanceRequested: true,
  assignedAgentUserId: 'agent-1',
  assignedAgentName: 'Test Agent',
  assignedAt: '2024-01-01T00:00:00Z',
  isAssignedToCurrentUser: true,
  isUnassigned: false,
  campaignName: 'Test Campaign',
  nextAction: 'Review campaign brief',
  timeline: [],
  recommendations: [],
  creativeSystems: [],
  assets: [],
  supplierBookings: [],
  deliveryReports: [],
  createdAt: '2024-01-01T00:00:00Z',

describe('CampaignStepper', () => {
  it('shows correct step progression for paid status', () => {
    const campaign = createMockCampaign('paid');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Prospect Acquisition" as active
    expect(screen.getByText('Prospect Acquisition')).toBeInTheDocument();
    expect(screen.getByText('Lead generated, agent assigned, package purchased, campaign created.')).toBeInTheDocument();
  });

  it('shows correct step progression for brief_submitted status', () => {
    const campaign = createMockCampaign('brief_submitted');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Brief Collection" as active
    expect(screen.getByText('Brief Collection')).toBeInTheDocument();
    expect(screen.getByText('Client fills and submits campaign brief.')).toBeInTheDocument();
  });

  it('shows correct step progression for review_ready status', () => {
    const campaign = createMockCampaign('review_ready');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Planning & Strategy" as active
    expect(screen.getByText('Planning & Strategy')).toBeInTheDocument();
    expect(screen.getByText('Agent selects planning mode, system generates recommendations, inventory checked, proposal prepared.')).toBeInTheDocument();
  });

  it('shows correct step progression for approved status', () => {
    const campaign = createMockCampaign('approved');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Client Review & Approval" as active
    expect(screen.getByText('Client Review & Approval')).toBeInTheDocument();
    expect(screen.getByText('Client reviews proposals, requests changes or approves.')).toBeInTheDocument();
  });

  it('shows correct step progression for creative_sent_to_client_for_approval status', () => {
    const campaign = createMockCampaign('creative_sent_to_client_for_approval');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Creative Development" as active
    expect(screen.getByText('Creative Development')).toBeInTheDocument();
    expect(screen.getByText('Creative team develops assets. Client reviews and approves.')).toBeInTheDocument();
  });

  it('shows correct step progression for booking_in_progress status', () => {
    const campaign = createMockCampaign('booking_in_progress');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Operational Fulfillment" as active
    expect(screen.getByText('Operational Fulfillment')).toBeInTheDocument();
    expect(screen.getByText('Bookings coordinated, assets prepared, suppliers confirmed.')).toBeInTheDocument();
  });

  it('shows correct step progression for launched status', () => {
    const campaign = createMockCampaign('launched');
    render(<CampaignStepper campaign={campaign} />);

    // Should show "Live Campaign Management" as active
    expect(screen.getByText('Live Campaign Management')).toBeInTheDocument();
    expect(screen.getByText('Campaign launched, performance tracked, reporting ongoing.')).toBeInTheDocument();
  });

  it('renders all 7 steps with correct icons', () => {
    const campaign = createMockCampaign('paid');
    render(<CampaignStepper campaign={campaign} />);

    // Check that all step labels are present
    expect(screen.getByText('Prospect Acquisition')).toBeInTheDocument();
    expect(screen.getByText('Brief Collection')).toBeInTheDocument();
    expect(screen.getByText('Planning & Strategy')).toBeInTheDocument();
    expect(screen.getByText('Client Review & Approval')).toBeInTheDocument();
    expect(screen.getByText('Creative Development')).toBeInTheDocument();
    expect(screen.getByText('Operational Fulfillment')).toBeInTheDocument();
    expect(screen.getByText('Live Campaign Management')).toBeInTheDocument();

    // Check that icons are present (they're text emojis)
    expect(screen.getByText('📋')).toBeInTheDocument(); // Prospect Acquisition
    expect(screen.getByText('✏️')).toBeInTheDocument(); // Brief Collection
    expect(screen.getByText('📝')).toBeInTheDocument(); // Planning & Strategy
    expect(screen.getByText('👁️')).toBeInTheDocument(); // Client Review
    expect(screen.getByText('🎨')).toBeInTheDocument(); // Creative Development
    expect(screen.getByText('📦')).toBeInTheDocument(); // Operational Fulfillment
    expect(screen.getByText('🚀')).toBeInTheDocument(); // Live Management
  });
});