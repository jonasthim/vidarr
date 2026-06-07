import { Outlet } from "react-router-dom";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { Sidebar } from "./Sidebar";
import { Topbar } from "./Topbar";

export function Layout(): JSX.Element {
  const queryClient = useQueryClient();
  const authStatusQuery = useQuery({
    queryKey: ["auth-status"],
    queryFn: api.getAuthStatus,
    refetchInterval: false,
    retry: false,
  });
  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["auth-status"] }),
  });

  const status = authStatusQuery.data;

  return (
    <div className="app">
      <Topbar />
      <Sidebar
        showLogout={status?.enabled === true}
        onLogout={() => logout.mutate()}
        username={status?.username}
      />
      <main className="main">
        <div className="page">
          <Outlet />
        </div>
      </main>
    </div>
  );
}
