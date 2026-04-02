import { useState } from 'react';
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { Link, useSearchParams } from 'react-router-dom';
import { MessageSquareText, SendHorizontal } from 'lucide-react';
import { useToast } from '../../components/ui/toast';
import { formatDate, titleCase } from '../../lib/utils';
import { advertifiedApi } from '../../services/advertifiedApi';
import {
  AgentPageShell,
  AgentQueryBoundary,
  queueTone,
} from './agentWorkspace';

export function AgentMessagesNotesPage() {
  const queryClient = useQueryClient();
  const { pushToast } = useToast();
  const [searchParams, setSearchParams] = useSearchParams();
  const [draft, setDraft] = useState('');
  const inboxQuery = useQuery({ queryKey: ['agent-message-inbox'], queryFn: advertifiedApi.getAgentMessageInbox });
  const selectedCampaignId = searchParams.get('campaignId')
    ?? inboxQuery.data?.[0]?.campaignId
    ?? null;

  const threadQuery = useQuery({
    queryKey: ['agent-message-thread', selectedCampaignId],
    queryFn: () => advertifiedApi.getAgentMessageThread(selectedCampaignId!),
    enabled: Boolean(selectedCampaignId),
  });

  const sendMessageMutation = useMutation({
    mutationFn: (body: string) => advertifiedApi.sendAgentMessage(selectedCampaignId!, body),
    onSuccess: async (thread) => {
      setDraft('');
      queryClient.setQueryData(['agent-message-thread', selectedCampaignId], thread);
      await queryClient.invalidateQueries({ queryKey: ['agent-message-inbox'] });
      pushToast({ title: 'Message sent.', description: 'The client can now reply in-app.' });
    },
    onError: (error) => {
      pushToast({ title: 'Could not send message.', description: error instanceof Error ? error.message : 'Please try again.' }, 'error');
    },
  });

  return (
    <AgentQueryBoundary query={inboxQuery} loadingLabel="Loading messages...">
      <AgentPageShell title="Messages" description="Real campaign conversations between agents and clients, with unread state, stored history, and in-app replies.">
        <section className="grid gap-6 xl:grid-cols-[360px_1fr]">
          <div className="rounded-[30px] border border-line bg-white shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
            <div className="border-b border-line px-6 py-5">
              <h3 className="text-lg font-semibold text-ink">Conversation inbox</h3>
              <p className="mt-2 text-sm text-ink-soft">Every campaign can now carry its own in-app client thread.</p>
            </div>
            <div className="max-h-[760px] overflow-y-auto p-4">
              {(inboxQuery.data ?? []).length > 0 ? (
                <div className="space-y-3">
                  {(inboxQuery.data ?? []).map((item) => (
                    <button
                      key={item.campaignId}
                      type="button"
                      onClick={() => setSearchParams({ campaignId: item.campaignId })}
                      className={`w-full rounded-[24px] border p-4 text-left transition ${
                        item.campaignId === selectedCampaignId
                          ? 'border-brand bg-brand-soft/40'
                          : 'border-line bg-slate-50/70 hover:border-brand/30 hover:bg-brand-soft/20'
                      }`}
                    >
                      <div className="flex items-start justify-between gap-3">
                        <div className="min-w-0">
                          <p className="truncate font-semibold text-ink">{item.campaignName}</p>
                          <p className="mt-1 truncate text-sm text-ink-soft">{item.clientName}</p>
                        </div>
                        {item.unreadCount > 0 ? (
                          <span className="rounded-full bg-brand px-2.5 py-1 text-xs font-semibold text-white">{item.unreadCount}</span>
                        ) : null}
                      </div>
                      <div className="mt-3 flex items-center gap-2">
                        <span className={`inline-flex rounded-full border px-3 py-1 text-[11px] font-semibold ${queueTone(item.campaignStatus === 'review_ready' ? 'waiting_on_client' : item.campaignStatus)}`}>
                          {titleCase(item.campaignStatus)}
                        </span>
                        <span className="text-xs text-ink-soft">{item.packageBandName}</span>
                      </div>
                      <p className="mt-3 text-sm text-ink-soft">
                        {item.lastMessagePreview ?? 'No messages yet. Open this thread to start the conversation.'}
                      </p>
                      <p className="mt-2 text-xs text-ink-soft">
                        {item.lastMessageAt ? `Last message ${formatDate(item.lastMessageAt)}` : 'No activity yet'}
                      </p>
                    </button>
                  ))}
                </div>
              ) : (
                <div className="rounded-[24px] border border-dashed border-line bg-slate-50 p-6 text-sm text-ink-soft">
                  No conversations exist yet. Once an agent or client sends the first in-app message, it will appear here.
                </div>
              )}
            </div>
          </div>

          <div className="rounded-[30px] border border-line bg-white shadow-[0_18px_50px_rgba(15,23,42,0.05)]">
            {selectedCampaignId ? (
              <AgentQueryBoundary query={threadQuery} loadingLabel="Loading thread...">
                {(() => {
                  const thread = threadQuery.data;
                  if (!thread) {
                    return null;
                  }

                  return (
                    <div className="flex h-full min-h-[760px] flex-col">
                      <div className="border-b border-line px-6 py-5">
                        <div className="flex flex-col gap-4 lg:flex-row lg:items-start lg:justify-between">
                          <div>
                            <div className="inline-flex items-center gap-2 text-xs font-semibold uppercase tracking-[0.22em] text-brand">
                              <MessageSquareText className="size-4" />
                              Campaign thread
                            </div>
                            <h3 className="mt-3 text-2xl font-semibold text-ink">{thread.campaignName}</h3>
                            <p className="mt-2 text-sm text-ink-soft">{thread.clientName} | {thread.clientEmail}</p>
                            <p className="mt-1 text-sm text-ink-soft">{thread.packageBandName} | {titleCase(thread.campaignStatus)}</p>
                          </div>
                          <div className="flex gap-3">
                            <Link to={`/agent/campaigns/${thread.campaignId}`} className="button-secondary px-4 py-2">Open campaign</Link>
                            <Link to={`/ai-studio?campaignId=${thread.campaignId}`} className="button-secondary px-4 py-2">Prefill AI Studio</Link>
                          </div>
                        </div>
                      </div>

                      <div className="flex-1 space-y-4 overflow-y-auto bg-slate-50/50 px-6 py-6">
                        {thread.messages.length > 0 ? thread.messages.map((message) => {
                          const isAgent = message.senderRole === 'agent';
                          return (
                            <div key={message.id} className={`flex ${isAgent ? 'justify-end' : 'justify-start'}`}>
                              <div className={`max-w-[720px] rounded-[24px] border px-5 py-4 ${
                                isAgent
                                  ? 'border-brand/20 bg-brand text-white'
                                  : 'border-line bg-white text-ink'
                              }`}>
                                <div className={`text-xs font-semibold uppercase tracking-[0.18em] ${isAgent ? 'text-white/75' : 'text-ink-soft'}`}>
                                  {message.senderName} | {titleCase(message.senderRole)}
                                </div>
                                <p className={`mt-2 whitespace-pre-wrap text-sm leading-7 ${isAgent ? 'text-white' : 'text-ink'}`}>{message.body}</p>
                                <p className={`mt-3 text-xs ${isAgent ? 'text-white/70' : 'text-ink-soft'}`}>
                                  {formatDate(message.createdAt)} {message.isRead ? ' | Read' : ''}
                                </p>
                              </div>
                            </div>
                          );
                        }) : (
                          <div className="rounded-[24px] border border-dashed border-line bg-white p-8 text-sm text-ink-soft">
                            No messages yet. Start the conversation from here and the client will be able to reply inside their campaign workspace.
                          </div>
                        )}
                      </div>

                      <div className="border-t border-line px-6 py-5">
                        <label className="block text-xs font-semibold uppercase tracking-[0.18em] text-ink-soft">Reply</label>
                        <textarea
                          value={draft}
                          onChange={(event) => setDraft(event.target.value)}
                          className="input-base mt-3 min-h-[140px]"
                          placeholder="Write a message to the client..."
                        />
                        <div className="mt-4 flex justify-end">
                          <button
                            type="button"
                            disabled={sendMessageMutation.isPending || !draft.trim()}
                            onClick={() => sendMessageMutation.mutate(draft)}
                            className="button-primary inline-flex items-center gap-2 px-5 py-3 disabled:cursor-not-allowed disabled:opacity-60"
                          >
                            <SendHorizontal className="size-4" />
                            {sendMessageMutation.isPending ? 'Sending...' : 'Send message'}
                          </button>
                        </div>
                      </div>
                    </div>
                  );
                })()}
              </AgentQueryBoundary>
            ) : (
              <div className="flex min-h-[760px] items-center justify-center px-8 text-center text-sm text-ink-soft">
                Select a campaign conversation from the inbox to open the thread.
              </div>
            )}
          </div>
        </section>
      </AgentPageShell>
    </AgentQueryBoundary>
  );
}
