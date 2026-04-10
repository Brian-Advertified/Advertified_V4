import { CheckCircle2, Circle, Clock, FileText, Lightbulb, Send, Users } from 'lucide-react';
import { useMemo } from 'react';
import type { CSSProperties } from 'react';
import type { Campaign } from '../../types/domain';

interface AgentStep {
  id: string;
  label: string;
  description: string;
  icon: React.ComponentType<{ className?: string }>;
}

interface AgentStepperProps {
  campaign: Campaign;
}

export function AgentStepper({ campaign }: AgentStepperProps) {
  const steps = useMemo((): AgentStep[] => [
    {
      id: 'lead',
      label: 'Lead',
      description: 'Newly paid campaign received',
      icon: Users,
    },
    {
      id: 'brief',
      label: 'Brief',
      description: 'Gather campaign requirements',
      icon: FileText,
    },
    {
      id: 'planning',
      label: 'Planning',
      description: 'Build media recommendations',
      icon: Lightbulb,
    },
    {
      id: 'review',
      label: 'Recommendation',
      description: 'Review and send the recommendation',
      icon: Clock,
    },
    {
      id: 'content',
      label: 'Content',
      description: 'Create content and get client sign-off',
      icon: Send,
    },
    {
      id: 'booking-live',
      label: 'Booking & Live',
      description: 'Book suppliers and prepare launch',
      icon: CheckCircle2,
    },
  ], []);

  const getCurrentStepIndex = (status: Campaign['status']): number => {
    switch (status) {
      case 'awaiting_purchase':
      case 'paid':
        return 0;
      case 'brief_in_progress':
      case 'brief_submitted':
        return 1;
      case 'planning_in_progress':
        return 2;
      case 'review_ready':
        return 3;
      case 'approved':
      case 'creative_sent_to_client_for_approval':
      case 'creative_changes_requested':
        return 4;
      case 'creative_approved':
      case 'booking_in_progress':
      case 'launched':
        return 5;
      default:
        return 0;
    }
  };

  const currentStepIndex = getCurrentStepIndex(campaign.status);
  const progressStyle = { '--step-progress-width': `${((currentStepIndex + 1) / steps.length) * 100}%` } as CSSProperties;

  return (
    <div className="w-full">
      <div className="mb-4 flex items-center justify-between text-sm font-medium text-gray-700">
        <span>Campaign Progress</span>
        <span className="text-brand">
          {currentStepIndex + 1} of {steps.length} steps
        </span>
      </div>

      {/* Progress Bar */}
      <div className="relative mb-6">
        <div className="h-2 w-full rounded-full bg-gray-200">
          <div
            className="step-progress-bar h-2 rounded-full bg-gradient-to-r from-brand to-amber-500 transition-all duration-500 ease-out"
            style={progressStyle}
          />
        </div>
      </div>

      {/* Steps */}
      <div className="grid grid-cols-2 gap-4 md:grid-cols-3 lg:grid-cols-6">
        {steps.map((step, index) => {
          const isCompleted = index < currentStepIndex;
          const isCurrent = index === currentStepIndex;

          return (
            <div
              key={step.id}
              className={`flex flex-col items-center space-y-2 rounded-lg p-3 transition-all duration-200 ${
                isCurrent
                  ? 'bg-brand/5 border border-brand/20 shadow-sm'
                  : isCompleted
                  ? 'bg-green-50 border border-green-200'
                  : 'bg-gray-50 border border-gray-200'
              }`}
            >
              <div className="flex flex-col items-center space-y-1">
                {isCompleted ? (
                  <CheckCircle2 className="h-6 w-6 text-green-600" />
                ) : isCurrent ? (
                  <step.icon className="h-6 w-6 text-brand" />
                ) : (
                  <Circle className="h-6 w-6 text-gray-400" />
                )}
                <span
                  className={`text-xs font-medium ${
                    isCompleted
                      ? 'text-green-700'
                      : isCurrent
                      ? 'text-brand'
                      : 'text-gray-500'
                  }`}
                >
                  {step.label}
                </span>
              </div>
              <p className="text-center text-xs text-gray-600 leading-tight">
                {step.description}
              </p>
            </div>
          );
        })}
      </div>
    </div>
  );
}
