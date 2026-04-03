using Advertified.App.Data.Entities;

namespace Advertified.App.Support;

internal static class EmailTemplateDefaults
{
    internal static IReadOnlyList<EmailTemplate> CreateDefaults()
    {
        var nowUtc = DateTime.UtcNow;

        return new List<EmailTemplate>
        {
            Build("user_activation", "Activate Your Advertified Account", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified account</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Activate your Advertified account</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{UserName}}, you are one step away from activating your Advertified account.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Once activated, you will set your password and continue directly into your workspace.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Activation details</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Account:</strong> {{UserName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Link expiry:</strong> {{ExpiresInHours}} hours</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Next step:</strong> Activate account and set password</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Use the secure button below to activate your account.
                      </p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{ActivationUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Activate your Advertified account</a>
                      </p>
                      <p style=""margin:20px 0 0;font-size:14px;line-height:1.7;color:#4b635a;"">
                        If you did not create this account, you can safely ignore this email.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("welcome", "Welcome to Advertified", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Welcome to Advertified</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your account is now active</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{UserName}}, your email has been verified and your Advertified account is ready to use.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        If you have not set your password yet, complete that step first, then sign in normally with your email and password.
                      </p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{SignInUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Continue to Advertified</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("password_reset", "Reset Your Advertified Password", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified security</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Reset your password</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{UserName}}, we received a request to reset your Advertified password.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Use the secure link below to choose a new password.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Reset details</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Account:</strong> {{UserName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Link expiry:</strong> {{ExpiresInHours}} hours</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Password reset requested</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{ResetUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Reset your password</a>
                      </p>
                      <p style=""margin:20px 0 0;font-size:14px;line-height:1.7;color:#4b635a;"">
                        If you did not request this, you can safely ignore this email.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("password-reset-success", "Your Advertified password was changed", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified security</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your password was reset successfully</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{UserName}}, this confirms that your Advertified password has just been changed.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        If you made this change, you can sign in with your new password below. If you did not, contact support immediately so we can help secure your account.
                      </p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{SignInUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Sign in securely</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("support-request", "Support request: {{Subject}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified internal update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">New support request</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">A signed-in customer submitted a support request from the dashboard.</p>
                      <table style=""border-collapse:collapse;width:100%;max-width:720px"">
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Category</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Category}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Subject</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Subject}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Customer</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{UserName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Email</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{UserEmail}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Company</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{CompanyName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Source</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Source}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Context</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ContextSummary}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Message</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Message}}</td></tr>
                      </table>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("partner-enquiry-notification", "New media partner enquiry from {{CompanyName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified internal update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">New media partner enquiry</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">A prospective media partner submitted an enquiry from the Advertified website.</p>
                      <table style=""border-collapse:collapse;width:100%;max-width:720px"">
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Contact name</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{FullName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Company</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{CompanyName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Email</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Email}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Phone</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Phone}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Partner type</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{PartnerType}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Inventory summary</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{InventorySummary}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Message</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Message}}</td></tr>
                      </table>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("proposal-delivery", "Your Advertified proposal {{ProposalReference}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your campaign proposal is ready</h1>
                      <p style=""margin:12px 0 0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Proposal reference <strong>{{ProposalReference}}</strong> has been prepared for your review.
                      </p>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;"">Hi {{RecipientName}},</p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Attached is your Advertified proposal PDF for <strong>{{CampaignName}}</strong>.
                        It includes the recommended mix, optional supporting channels, indicative pricing, and next steps.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Proposal snapshot</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Primary package:</strong> {{PrimaryPackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{ProposalReference}}</p>
                      </div>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        If you return to Advertified later, you can reopen this proposal using your reference and email address.
                        Use <strong>{{ProposalReference}}</strong> and <strong>{{ContactEmail}}</strong>.
                      </p>
                      <p style=""margin:0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        When you are ready, sign in or register to continue refining the plan and move into checkout.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("proposal-reminder", "{{ReminderSubjectLine}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified campaigns</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your proposal is still ready when you are</h1>
                      <p style=""margin:12px 0 0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Proposal reference <strong>{{ProposalReference}}</strong> for <strong>{{CampaignName}}</strong>.
                      </p>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 14px;font-size:15px;line-height:1.7;"">Hi {{RecipientName}},</p>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">{{ReminderIntro}}</p>
                      <div style=""margin:18px 0 20px;"">
                        <a href=""{{ReopenUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#d7f0e4;color:#123A33;text-decoration:none;font-weight:700;"">
                          Reopen proposal
                        </a>
                      </div>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Campaign snapshot</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Prepared for:</strong> {{CompanyName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Lead package:</strong> {{PrimaryPackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Indicative total:</strong> {{EstimatedTotal}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{ProposalReference}}</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">{{ReminderClosing}}</p>
                      <p style=""margin:0 0 12px;font-size:14px;line-height:1.7;color:#4b635a;"">
                        We have attached the latest PDF again for convenience.
                      </p>
                      <p style=""margin:0;font-size:14px;line-height:1.7;color:#4b635a;"">
                        If you do not need help right now, no action is required. We will simply keep the proposal ready for you.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("proposal-callback-request", "Callback request for proposal {{ProposalReference}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified internal update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Callback request received</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">A customer requested a strategist callback from the proposal preview.</p>
                      <table style=""border-collapse:collapse;width:100%;max-width:720px"">
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Proposal reference</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ProposalReference}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Contact</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ContactName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Email</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ContactEmail}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Phone</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ContactPhone}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Company</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{CompanyName}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Campaign</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{CampaignName}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Lead package</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{PrimaryPackageName}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Indicative total</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{EstimatedTotal}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Notes</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{Notes}}</td></tr>
                      </table>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("invoice-delivery", "Your paid tax invoice for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your paid tax invoice is ready</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Payment has been confirmed for <strong>{{CampaignName}}</strong>, and we have attached your paid tax invoice to this email.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Invoice:</strong> {{InvoiceNumber}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign plan:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Amount paid:</strong> {{Amount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{PaymentReference}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Paid, awaiting activation</p>
                      </div>
                      <p style=""margin:0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Keep this invoice for your records. If you need help with artwork, activation timing, or campaign fulfilment, simply reply to this email and the Advertified team will assist.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-assigned", "Your campaign has been assigned to a strategist for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified strategist update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your campaign has been assigned</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, your campaign <strong>{{CampaignName}}</strong> has now been assigned to an Advertified strategist.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        This means your package is now in the hands of the team member who will review the brief, shape the recommendation, and guide it through to client review.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Assignment summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>{{BudgetLabel}}:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Assigned to strategist</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        You do not need to take any action right now. We will let you know again when your strategist starts active recommendation work and once your proposals are ready to review.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          View campaign
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("agent-working", "Your strategist has started working on {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified strategist update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Recommendation work has started</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, your strategist has now started active recommendation work on <strong>{{CampaignName}}</strong>.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        At this stage we are shaping the recommended media mix, checking real supplier inventory, and aligning the draft against your selected package, budget, and campaign brief.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Work in progress</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Strategist actively working</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        We will contact you again once the recommendation set is ready for review. Until then, your campaign page remains the best place to follow progress.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Track campaign progress
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("agent-area-campaign-assigned", "New campaign assigned to your area: {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified agent alert</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">A new campaign has been routed to your area</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{AgentName}}, a newly purchased campaign has been matched to your assigned area coverage.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Assignment summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Client:</strong> {{ClientName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Assigned area:</strong> {{AreaName}} ({{AreaCode}})</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Open the agent workspace to review the campaign, confirm the brief context, and begin support for this client.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{AgentCampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Open campaign
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("recommendation-ready", "Your Advertified recommendation options are ready for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified recommendations</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your campaign recommendations are ready to review</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 14px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, your Advertified strategist has prepared recommendation options for <strong>{{CampaignName}}</strong>.
                      </p>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        {{ProposalSummary}} Approve your preferred option below, or open the full review page if you want to compare everything first.
                      </p>
                      {{ProposalAcceptButtonsBlock}}
                      <div style=""margin:18px 0 20px;"">
                        <a href=""{{ReviewUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Review recommendation options
                        </a>
                      </div>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Recommendation summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Options prepared:</strong> {{ProposalCount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Ready for your review</p>
                      </div>
                      {{AgentMessageBlock}}
                      {{RecommendationPackBlock}}
                      <p style=""margin:0;font-size:14px;line-height:1.7;color:#4b635a;"">
                        If you need help before making a decision, reply to this email or contact <a href=""mailto:support@advertified.com"" style=""color:#123A33;"">support@advertified.com</a>.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("brief-submitted", "Your campaign brief was submitted for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified campaign brief</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your brief has been submitted</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, we have received the campaign brief for <strong>{{CampaignName}}</strong>.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Our planning workflow now has the information it needs to start shaping a recommendation around your selected package, budget, and campaign intent.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Submission summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Brief submitted</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        You do not need to do anything else right now. We will let you know as soon as your recommendation is being prepared and again when it is ready for review.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          View campaign
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("recommendation-preparing", "We are preparing your recommendation for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified planning</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your recommendation is being prepared</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, we have started preparing the recommendation for <strong>{{CampaignName}}</strong>.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        The Advertified planning workflow is now translating your brief into a campaign recommendation that fits your selected package, budget band, and media constraints.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Planning summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Recommendation being prepared</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        We will send you another update as soon as the recommendation is ready to review. Until then, your campaign page will show the latest progress.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Track campaign progress
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("recommendation-approved", "Your recommendation has been approved for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified approval</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your recommendation is approved</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, your recommendation for <strong>{{CampaignName}}</strong> has been approved successfully.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        This means the strategy and draft media plan have been signed off and the campaign can now move into the fulfilment and activation stage.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Approval summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Recommendation approved</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Our team will now begin coordinating the next operational steps to move your campaign toward activation.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Open campaign
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("activation-in-progress", "Activation is now in progress for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified activation</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your campaign is moving into activation</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, activation is now in progress for <strong>{{CampaignName}}</strong>.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Advertified is now moving from recommendation approval into the operational fulfilment phase, including confirming the plan, artwork readiness, and the next media execution steps where applicable.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Activation summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Activation in progress</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        If we need anything further from you, we will reach out directly. Otherwise, you can follow progress from your campaign page at any time.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          View campaign progress
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("refund-processed", "Refund processed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your refund has been processed</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Hi {{ClientName}}, a refund has been processed for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Refund amount:</strong> {{RefundAmount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Refund status:</strong> {{RefundStatus}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Policy stage:</strong> {{RefundPolicyLabel}}</p>
                      </div>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;""><strong>Reason:</strong> {{RefundReason}}</p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open campaign</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-paused", "Campaign paused: {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified operations</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">{{CampaignName}} has been paused</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Hi {{ClientName}}, we have paused work on <strong>{{CampaignName}}</strong>.</p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;""><strong>Reason:</strong> {{PauseReason}}</p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open campaign</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-resumed", "Campaign resumed: {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified operations</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">{{CampaignName}} has resumed</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Hi {{ClientName}}, work on <strong>{{CampaignName}}</strong> has resumed.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Paused days added back:</strong> {{PausedDays}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Days left:</strong> {{DaysLeft}}</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open campaign</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-live", "Your Advertified campaign is now live: {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified live campaign</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">{{CampaignName}} is now live</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{ClientName}}, operations has activated <strong>{{CampaignName}}</strong> and the campaign is now live.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Launch summary</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign:</strong> {{CampaignName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Selected budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Live</p>
                      </div>
                      <p style=""margin:0 0 18px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        You can follow the latest status and team updates from your campaign workspace at any time.
                      </p>
                      <div style=""margin:22px 0 20px;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;border-radius:14px;background:#123A33;color:#ffffff;text-decoration:none;font-weight:700;"">
                          Open campaign
                        </a>
                      </div>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-booking-confirmed", "Supplier booking confirmed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified delivery</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">A supplier booking has been logged for {{CampaignName}}</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Hi {{ClientName}}, we have recorded a supplier execution update for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Supplier / station:</strong> {{SupplierOrStation}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Channel:</strong> {{Channel}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> {{BookingStatus}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Committed amount:</strong> {{CommittedAmount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Live window:</strong> {{LiveWindow}}</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open campaign</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-report-available", "Delivery update available for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified live reporting</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">{{Headline}}</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Hi {{ClientName}}, a new campaign delivery report has been added for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Report type:</strong> {{ReportType}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reported at:</strong> {{ReportedAt}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Impressions:</strong> {{Impressions}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Plays / spots:</strong> {{PlaysOrSpots}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Spend delivered:</strong> {{SpendDelivered}}</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{CampaignUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open campaign</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("campaign-message-notification", "New campaign message for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified messages</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">You have a new message on {{CampaignName}}</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Hi {{RecipientName}}, {{SenderName}} ({{SenderRole}}) sent a new message on <strong>{{CampaignName}}</strong>.
                      </p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <div style=""font-size:12px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Message preview</div>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.7;color:#12211D;"">{{MessagePreview}}</p>
                        <p style=""margin:10px 0 0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{ThreadUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open message thread</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("payment-approved-lula", "Payment confirmed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your Lula payment has been confirmed</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">We have confirmed payment for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Invoice:</strong> {{InvoiceNumber}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign plan:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Amount settled:</strong> {{Amount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{PaymentReference}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Paid, awaiting activation</p>
                      </div>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Advertified will now move your campaign into activation and fulfilment. If you need artwork support or flighting updates, reply to this email and our team will help you.
                      </p>
                      {{AdminNoteBlock}}
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("payment-declined-lula", "Payment could not be confirmed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your Lula payment could not be confirmed</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">We could not confirm the Lula payment for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Invoice:</strong> {{InvoiceNumber}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Campaign plan:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Amount:</strong> {{Amount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Current status:</strong> Payment not confirmed</p>
                      </div>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        You can still work with the Advertified team to retry settlement or choose a different payment route. Reply to this email if you would like us to help you complete the campaign.
                      </p>
                      {{AdminNoteBlock}}
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("payment-approved-vodapay", "Payment confirmed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your VodaPay payment was successful</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">We have confirmed your VodaPay payment for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Amount settled:</strong> {{Amount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{PaymentReference}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Paid, awaiting activation</p>
                      </div>
                      <p style=""margin:0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Your campaign is now moving into activation. Your paid tax invoice will arrive as a separate attachment email for your records.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("payment-failed-vodapay", "Payment failed for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified billing</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your VodaPay payment was not successful</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">We could not confirm your VodaPay payment for <strong>{{CampaignName}}</strong>.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Amount:</strong> {{Amount}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Reference:</strong> {{PaymentReference}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Status:</strong> Payment failed</p>
                      </div>
                      <p style=""margin:0;font-size:15px;line-height:1.7;color:#4b635a;"">
                        You can retry payment or contact the Advertified team if you want help completing the campaign.
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("studio-ready", "Advertified Studio is ready for {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified Studio</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">Your studio is ready</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Your campaign <strong>{{CampaignName}}</strong> is now ready inside Advertified Studio following {{ReadinessSource}}.
                      </p>
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">
                        Open the studio to review your creative direction, refine campaign messaging, and continue preparing your campaign assets.
                      </p>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{StudioUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open Advertified Studio</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc)
                ,
            Build("creative-queue-update", "Creative queue update: {{CampaignName}}", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified internal update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">{{EventTitle}}</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">Campaign <strong>{{CampaignName}}</strong> has a new workflow update.</p>
                      <div style=""margin:24px 0;padding:18px 20px;border:1px solid #d8e9e1;border-radius:18px;background:#f8fcfa;"">
                        <p style=""margin:0;font-size:15px;line-height:1.6;""><strong>Package:</strong> {{PackageName}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Budget:</strong> {{Budget}}</p>
                        <p style=""margin:6px 0 0;font-size:15px;line-height:1.6;""><strong>Update:</strong> {{EventBody}}</p>
                      </div>
                      <p style=""margin:24px 0 0;"">
                        <a href=""{{ActionUrl}}"" style=""display:inline-block;padding:12px 18px;background:#123A33;color:#ffffff;text-decoration:none;border-radius:12px;font-weight:700;"">Open workflow</a>
                      </p>
                    </div>
                  </div>
                </div>
                ", nowUtc),
            Build("admin-sale-alert", "New package sale: {{CampaignName}} ({{PaymentProvider}})", @"
                <div style=""background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;"">
                  <div style=""max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;"">
                    <div style=""padding:28px 32px;background:linear-gradient(180deg,#eefbf5 0%, #ffffff 100%);border-bottom:1px solid #d8e9e1;"">
                      <div style=""font-size:14px;letter-spacing:0.12em;text-transform:uppercase;color:#4b635a;font-weight:700;"">Advertified internal update</div>
                      <h1 style=""margin:12px 0 0;font-size:30px;line-height:1.2;color:#123A33;"">New package sale alert</h1>
                    </div>
                    <div style=""padding:28px 32px;"">
                      <p style=""margin:0 0 16px;font-size:15px;line-height:1.7;color:#4b635a;"">A new package order was created and requires tracking.</p>
                      <table style=""border-collapse:collapse;width:100%;max-width:720px"">
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Client</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ClientName}} ({{ClientEmail}})</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Campaign</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{CampaignName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Package</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{PackageName}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Selected budget</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{SelectedBudget}}</td></tr>
                    <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Charged amount</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ChargedAmount}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Provider</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{PaymentProvider}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Order status</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{PaymentStatus}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Action</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{ActionNote}}</td></tr>
                      <tr><td style=""padding:8px 12px;border:1px solid #d1d5db;font-weight:600"">Admin URL</td><td style=""padding:8px 12px;border:1px solid #d1d5db"">{{AdminUrl}}</td></tr>
                      </table>
                    </div>
                  </div>
                </div>
                ", nowUtc)
        };
    }

    private static EmailTemplate Build(string templateName, string subjectTemplate, string bodyHtmlTemplate, DateTime nowUtc)
    {
        return new EmailTemplate
        {
            Id = Guid.NewGuid(),
            TemplateName = templateName,
            SubjectTemplate = subjectTemplate.Trim(),
            BodyHtmlTemplate = WrapForResponsiveEmail(bodyHtmlTemplate),
            IsActive = true,
            CreatedAtUtc = nowUtc,
            UpdatedAtUtc = nowUtc
        };
    }

    private static string WrapForResponsiveEmail(string bodyHtmlTemplate)
    {
        var normalizedBody = bodyHtmlTemplate.Trim()
            .Replace(
                "<div style=\"background:#f4fbf8;padding:32px;font-family:Arial,sans-serif;color:#12211D;\">",
                "<div class=\"email-shell\" style=\"background:#f4fbf8;padding:24px 12px;font-family:Arial,sans-serif;color:#12211D;\">",
                StringComparison.Ordinal)
            .Replace(
                "<div style=\"max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;\">",
                "<div class=\"email-card\" style=\"width:100%;max-width:680px;margin:0 auto;background:#ffffff;border:1px solid #d8e9e1;border-radius:24px;overflow:hidden;\">",
                StringComparison.Ordinal)
            .Replace(
                "padding:28px 32px;",
                "padding:24px 20px;",
                StringComparison.Ordinal)
            .Replace(
                "font-size:30px;",
                "font-size:28px;",
                StringComparison.Ordinal)
            .Replace(
                "style=\"display:inline-block;padding:12px 18px;",
                "class=\"email-button\" style=\"display:inline-block;width:100%;max-width:320px;box-sizing:border-box;text-align:center;padding:14px 18px;",
                StringComparison.Ordinal)
            .Replace(
                "style=\"border-collapse:collapse;width:100%;max-width:720px\"",
                "class=\"email-table\" style=\"border-collapse:collapse;width:100%;max-width:720px;table-layout:fixed;word-break:break-word\"",
                StringComparison.Ordinal);

        return $@"<!doctype html>
<html lang=""en"">
  <head>
    <meta charset=""utf-8"">
    <meta name=""viewport"" content=""width=device-width, initial-scale=1.0"">
    <meta name=""x-apple-disable-message-reformatting"">
    <style>
      body {{
        margin: 0;
        padding: 0;
        background: #f4fbf8;
        -webkit-text-size-adjust: 100%;
        -ms-text-size-adjust: 100%;
      }}

      table {{
        border-spacing: 0;
      }}

      img {{
        border: 0;
        display: block;
        max-width: 100%;
        height: auto;
      }}

      .email-root {{
        width: 100%;
      }}

      .email-table td {{
        word-break: break-word;
      }}

      @media only screen and (max-width: 640px) {{
        .email-shell {{
          padding: 12px !important;
        }}

        .email-card {{
          border-radius: 18px !important;
        }}

        .email-card h1 {{
          font-size: 24px !important;
        }}

        .email-card p,
        .email-card li,
        .email-card td,
        .email-card div {{
          max-width: 100% !important;
        }}

        .email-button {{
          max-width: none !important;
          width: 100% !important;
          display: block !important;
        }}
      }}
    </style>
  </head>
  <body>
    <div class=""email-root"">
      {normalizedBody}
    </div>
  </body>
</html>";
    }
}

