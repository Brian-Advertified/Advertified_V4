import React from 'react';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { AgentStepper } from '../src/components/agent/AgentStepper';
import type { Campaign } from '../src/types/domain';

const mockCampaign = (status: Campaign['status']): Campaign => ({
  id: 'test-campaign',
  userId: 'test-user',
  clientName: 'Test Client',
  clientEmail: 'client@test.com',
  businessName: 'Test Business',
  packageOrderId: 'test-order',
  packageBandId: 'test-package',
  packageBandName: 'Test Package',
  selectedBudget: 5000,
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

describe('AgentStepper', () => {
  it('renders all workflow steps', () => {
    render(<AgentStepper campaign={mockCampaign('newly_paid')} />);

    expect(screen.getByText('Lead')).toBeInTheDocument();
    expect(screen.getByText('Brief')).toBeInTheDocument();
    expect(screen.getByText('Planning')).toBeInTheDocument();
    expect(screen.getByText('Recommendation')).toBeInTheDocument();
    expect(screen.getByText('Content')).toBeInTheDocument();
    expect(screen.getByText('Booking & Live')).toBeInTheDocument();
  });

  it('shows correct progress for paid (step 1)', () => {
    render(<AgentStepper campaign={mockCampaign('paid')} />);

    expect(screen.getByText('1 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Newly paid campaign received')).toBeInTheDocument();
  });

  it('shows correct progress for brief_in_progress (step 2)', () => {
    render(<AgentStepper campaign={mockCampaign('brief_in_progress')} />);

    expect(screen.getByText('2 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Gather campaign requirements')).toBeInTheDocument();
  });

  it('shows correct progress for brief_submitted (step 2)', () => {
    render(<AgentStepper campaign={mockCampaign('brief_submitted')} />);

    expect(screen.getByText('2 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Gather campaign requirements')).toBeInTheDocument();
  });

  it('shows correct progress for planning_in_progress (step 3)', () => {
    render(<AgentStepper campaign={mockCampaign('planning_in_progress')} />);

    expect(screen.getByText('3 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Build media recommendations')).toBeInTheDocument();
  });

  it('shows correct progress for review_ready (step 4)', () => {
    render(<AgentStepper campaign={mockCampaign('review_ready')} />);

    expect(screen.getByText('4 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Review and send the recommendation')).toBeInTheDocument();
  });

  it('shows correct progress for approved (step 5)', () => {
    render(<AgentStepper campaign={mockCampaign('approved')} />);

    expect(screen.getByText('5 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Create content and get client sign-off')).toBeInTheDocument();
  });

  it('shows correct progress for creative_sent_to_client_for_approval (step 5)', () => {
    render(<AgentStepper campaign={mockCampaign('creative_sent_to_client_for_approval')} />);

    expect(screen.getByText('5 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Create content and get client sign-off')).toBeInTheDocument();
  });

  it('shows correct progress for booking_in_progress (step 6)', () => {
    render(<AgentStepper campaign={mockCampaign('booking_in_progress')} />);

    expect(screen.getByText('6 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Book suppliers and prepare launch')).toBeInTheDocument();
  });

  it('shows correct progress for launched (step 6)', () => {
    render(<AgentStepper campaign={mockCampaign('launched')} />);

    expect(screen.getByText('6 of 6 steps')).toBeInTheDocument();
    expect(screen.getByText('Book suppliers and prepare launch')).toBeInTheDocument();
  });

  it('displays step descriptions correctly', () => {
    render(<AgentStepper campaign={mockCampaign('newly_paid')} />);

    expect(screen.getByText('Newly paid campaign received')).toBeInTheDocument();
    expect(screen.getByText('Gather campaign requirements')).toBeInTheDocument();
    expect(screen.getByText('Build media recommendations')).toBeInTheDocument();
    expect(screen.getByText('Review and send the recommendation')).toBeInTheDocument();
    expect(screen.getByText('Create content and get client sign-off')).toBeInTheDocument();
    expect(screen.getByText('Book suppliers and prepare launch')).toBeInTheDocument();
  });

  it('renders progress bar with correct width', () => {
    render(<AgentStepper campaign={mockCampaign('planning_in_progress')} />);

    // For step 3 (planning_in_progress), progress should be 3/6 = 50%
    const progressBar = document.querySelector('[style*="width"]');
    expect(progressBar).toHaveStyle({ width: '50%' });
  });
});
