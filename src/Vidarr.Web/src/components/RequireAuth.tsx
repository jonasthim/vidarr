import { Navigate, Outlet, useLocation } from "react-router-dom";
import { useEffect } from "react";
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function RequireAuth(): JSX.Element {
  const location = useLocation();
  const authStatusQuery = useQuery({
    queryKey: ["auth-status"],
    queryFn: api.getAuthStatus,
    refetchInterval: false,
    retry: false,
  });

  // After a successful forms-auth login the SPA may still be running with an
  // empty window.VIDARR_API_KEY (production HTML deliberately omits the key
  // until the user is authenticated). Pull it from the API once we know the
  // session is live, so panels like Settings → Security can render it.
  useEffect(() => {
    const status = authStatusQuery.data;
    const w = window as { VIDARR_API_KEY?: string };
    if (status?.authenticated && !w.VIDARR_API_KEY) {
      api
        .getApiKey()
        .then((r) => {
          w.VIDARR_API_KEY = r.apiKey;
        })
        .catch(() => {
          /* leave VIDARR_API_KEY empty; cookie auth still covers /api/v1/* */
        });
    }
  }, [authStatusQuery.data]);

  if (authStatusQuery.isLoading) {
    return <div className="loading-state">Loading…</div>;
  }

  const status = authStatusQuery.data;
  const requiresLogin = status?.enabled === true && status?.authenticated === false;
  if (requiresLogin) {
    return <Navigate to="/login" state={{ from: location }} replace />;
  }
  return <Outlet />;
}
