import { useMemo } from 'react';
import { cn } from '../../lib/utils';
import type { Campaign } from '../../types/domain';

interface CampaignStep {
  id: string;
  label: string;
  icon: string;
  description: string;
  status: 'pending' | 'active' | 'completed';
}

interface CampaignStepperProps {
  campaign: Campaign;
  className?: string;
}

export function CampaignStepper({ campaign, className }: CampaignStepperProps) {
  const steps = useMemo((): CampaignStep[] => {
    // Map campaign status to workflow step index
    const getCurrentStepIndex = (status: Campaign['status']): number => {
      switch (status) {
        case 'awaiting_purchase':
          return 0;
        case 'paid':
          return 0; // Still in prospect acquisition until brief starts
        case 'brief_in_progress':
        case 'brief_submitted':
          return 1; // Brief collection
        case 'planning_in_progress':
        case 'review_ready':
          return 2; // Planning & Strategy
        case 'approved':
          return 3; // Client Review & Approval (completed)
        case 'creative_sent_to_client_for_approval':
        case 'creative_changes_requested':
          return 4; // Creative Development (in progress)
        case 'creative_approved':
        case 'booking_in_progress':
          return 5; // Operational Fulfillment
        case 'launched':
          return 6; // Live Campaign Management
        default:
          return 0;
      }
    };

    const currentStepIndex = getCurrentStepIndex(campaign.status);

    const allSteps: Omit<CampaignStep, 'status'>[] = [
      {
        id: 'prospect-acquisition',
        label: 'Prospect Acquisition',
        icon: '📋',
        description: 'Lead generated, agent assigned, package purchased, campaign created. Emails sent to client, agent, admin.',
      },
      {
        id: 'brief-collection',
        label: 'Brief Collection',
        icon: '✏️',
        description: 'Client fills and submits campaign brief. Confirmation email sent to client.',
      },
      {
        id: 'planning-strategy',
        label: 'Planning & Strategy',
        icon: '📝',
        description: 'Agent selects planning mode, system generates recommendations, inventory checked, proposal prepared. Emails notify client of workflow progress.',
      },
      {
        id: 'client-review',
        label: 'Client Review & Approval',
        icon: '👁️',
        description: 'Client reviews proposals, requests changes or approves. Payment confirmations sent. Campaign ready for creative.',
      },
      {
        id: 'creative-development',
        label: 'Creative Development',
        icon: '🎨',
        description: 'Creative team develops assets. Client reviews and approves. Emails for studio readiness, review, approval.',
      },
      {
        id: 'operational-fulfillment',
        label: 'Operational Fulfillment',
        icon: '📦',
        description: 'Bookings coordinated, assets prepared, suppliers confirmed. Emails track activation and supplier confirmations.',
      },
      {
        id: 'live-management',
        label: 'Live Campaign Management',
        icon: '🚀',
        description: 'Campaign launched, performance tracked, reporting ongoing. Emails notify client of updates, messages, reports.',
      },
    ];

    return allSteps.map((step, index) => ({
      ...step,
      status: index < currentStepIndex ? 'completed' : index === currentStepIndex ? 'active' : 'pending',
    }));
  }, [campaign.status]);

  const progressWidth = useMemo(() => {
    const currentStepIndex = steps.findIndex(step => step.status === 'active');
    if (currentStepIndex === -1) return 0;
    return (currentStepIndex / (steps.length - 1)) * 100;
  }, [steps]);

  return (
    <div className={cn("w-full", className)}>
      <h2 className="text-xl font-semibold mb-6 text-ink">Campaign Progress</h2>

      {/* Progress Line */}
      <div className="relative mb-8">
        <div className="absolute top-6 left-0 w-full h-1 bg-gray-200 rounded"></div>
        <div
          className="absolute top-6 left-0 h-1 bg-brand rounded transition-all duration-500"
          style={{ width: `${progressWidth}%` }}
        ></div>

        {/* Steps */}
        <div className="flex justify-between relative">
          {steps.map((step, index) => (
            <div
              key={step.id}
              className="step text-center cursor-pointer group flex-1"
              data-step={index}
            >
              <div className={cn(
                "step-circle w-12 h-12 rounded-full flex items-center justify-center mx-auto text-lg transition-all duration-300 group-hover:scale-110",
                {
                  'bg-gray-300 text-gray-600': step.status === 'pending',
                  'bg-orange-500 text-white': step.status === 'active',
                  'bg-green-600 text-white': step.status === 'completed',
                }
              )}>
                <span className="step-icon">{step.icon}</span>
              </div>
              <p className="step-label mt-2 text-xs font-medium text-ink-soft leading-tight max-w-20 mx-auto">
                {step.label}
              </p>
            </div>
          ))}
        </div>
      </div>

      {/* Current Step Details */}
      <div className="p-4 bg-gray-50 rounded-lg border border-gray-200">
        {(() => {
          const currentStep = steps.find(step => step.status === 'active') || steps[0];
          return (
            <>
              <h3 className="text-lg font-semibold mb-2 text-ink flex items-center gap-2">
                <span>{currentStep.icon}</span>
                {currentStep.label}
              </h3>
              <p className="text-sm text-ink-soft leading-relaxed">
                {currentStep.description}
              </p>
            </>
          );
        })()}
      </div>
    </div>
  );
}
