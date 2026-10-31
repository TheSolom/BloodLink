import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import * as React from "react";

export function createQueryClient(options?: { retry?: boolean | number }) {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 1000 * 60,
        retry: options?.retry ?? 1,
        refetchOnWindowFocus: false,
      },
    },
  });
}

export function Providers({
  children,
  client,
}: {
  children: React.ReactNode;
  client?: QueryClient;
}) {
  const [queryClient] = React.useState(() => client ?? createQueryClient());

  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
}
