import { type ErrorInfo, type ReactNode, Component } from 'react';

type AppErrorBoundaryProps = {
  children: ReactNode;
};

type AppErrorBoundaryState = {
  error: Error | null;
};

export class AppErrorBoundary extends Component<AppErrorBoundaryProps, AppErrorBoundaryState> {
  state: AppErrorBoundaryState = {
    error: null,
  };

  static getDerivedStateFromError(error: Error): AppErrorBoundaryState {
    return { error };
  }

  componentDidCatch(error: Error, errorInfo: ErrorInfo) {
    console.error('AppErrorBoundary', error, errorInfo);
  }

  private handleRetry = () => {
    this.setState({ error: null });
  };

  render() {
    if (!this.state.error) {
      return this.props.children;
    }

    return (
      <div className="min-h-screen bg-canvas px-4 py-10 sm:px-6 lg:px-8">
        <div className="mx-auto flex min-h-[70vh] w-full max-w-3xl items-center justify-center">
          <section className="panel flex w-full flex-col gap-5 px-6 py-8 text-left sm:px-8">
            <div className="pill bg-danger-soft text-danger">Something went wrong</div>
            <div className="space-y-3">
              <h1 className="text-2xl font-semibold text-ink sm:text-3xl">We hit an unexpected app error.</h1>
              <p className="max-w-2xl text-sm leading-7 text-ink-soft">
                You can retry this screen right away. If the problem keeps happening, refresh the page and we will
                reload the latest workspace state.
              </p>
            </div>
            <div className="flex flex-wrap gap-3">
              <button
                type="button"
                onClick={this.handleRetry}
                className="inline-flex items-center justify-center rounded-full bg-ink px-5 py-3 text-sm font-semibold text-white transition hover:bg-brand"
              >
                Retry screen
              </button>
              <button
                type="button"
                onClick={() => window.location.reload()}
                className="inline-flex items-center justify-center rounded-full border border-line px-5 py-3 text-sm font-semibold text-ink transition hover:border-brand hover:text-brand"
              >
                Reload app
              </button>
            </div>
          </section>
        </div>
      </div>
    );
  }
}
