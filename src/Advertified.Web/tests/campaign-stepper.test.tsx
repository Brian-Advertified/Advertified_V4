import React from 'react';
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { CampaignStepper } from '../src/components/campaign/CampaignStepper';
import type { Campaign } from '../src/types/domain';

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
});

describe('CampaignStepper', () => {
  it('shows correct step progression for paid status', () => {
    const campaign = createMockCampaign('paid');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Prospect Acquisition/i })).toBeInTheDocument();
    expect(screen.getByText(/Lead generated, agent assigned, package purchased, campaign created\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for brief_submitted status', () => {
    const campaign = createMockCampaign('brief_submitted');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Brief Collection/i })).toBeInTheDocument();
    expect(screen.getByText(/Client fills and submits campaign brief\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for review_ready status', () => {
    const campaign = createMockCampaign('review_ready');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Planning & Strategy/i })).toBeInTheDocument();
    expect(screen.getByText(/Agent selects planning mode, system generates recommendations, inventory checked, proposal prepared\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for approved status', () => {
    const campaign = createMockCampaign('approved');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Creative In Progress/i })).toBeInTheDocument();
    expect(screen.getByText(/Your campaign content is being prepared for your review\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for creative_sent_to_client_for_approval status', () => {
    const campaign = createMockCampaign('creative_sent_to_client_for_approval');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Creative In Progress/i })).toBeInTheDocument();
    expect(screen.getByText(/Your campaign content is being prepared for your review\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for booking_in_progress status', () => {
    const campaign = createMockCampaign('booking_in_progress');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Operational Fulfillment/i })).toBeInTheDocument();
    expect(screen.getByText(/After the client approves the content, bookings are coordinated and suppliers are confirmed\./i)).toBeInTheDocument();
  });

  it('shows correct step progression for launched status', () => {
    const campaign = createMockCampaign('launched');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getByRole('heading', { level: 3, name: /Live Campaign Management/i })).toBeInTheDocument();
    expect(screen.getByText(/Campaign launched, performance tracked, reporting ongoing\./i)).toBeInTheDocument();
  });

  it('renders all 7 step labels', () => {
    const campaign = createMockCampaign('paid');
    render(<CampaignStepper campaign={campaign} />);

    expect(screen.getAllByText('Prospect Acquisition').length).toBeGreaterThan(0);
    expect(screen.getByText('Brief Collection')).toBeInTheDocument();
    expect(screen.getByText('Planning & Strategy')).toBeInTheDocument();
    expect(screen.getByText('Client Review & Approval')).toBeInTheDocument();
    expect(screen.getByText('Creative In Progress')).toBeInTheDocument();
    expect(screen.getByText('Operational Fulfillment')).toBeInTheDocument();
    expect(screen.getByText('Live Campaign Management')).toBeInTheDocument();
  });
});
