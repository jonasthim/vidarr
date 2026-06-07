import { FormEvent, useState } from "react";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";

type Props = {
  onSuccess: () => void;
};

export function LoginPage({ onSuccess }: Props): JSX.Element {
  const queryClient = useQueryClient();
  const [username, setUsername] = useState("");
  const [password, setPassword] = useState("");
  const mutation = useMutation({
    mutationFn: () => api.login(username, password),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-status"] });
      onSuccess();
    },
  });

  const submit = (e: FormEvent) => {
    e.preventDefault();
    mutation.mutate();
  };

  return (
    <div className="login-shell">
      <form className="login-card" onSubmit={submit}>
        <h1>Vidarr</h1>
        <p className="muted">Sign in to continue</p>
        <label>
          <span>Username</span>
          <input
            type="text"
            autoComplete="username"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            required
          />
        </label>
        <label>
          <span>Password</span>
          <input
            type="password"
            autoComplete="current-password"
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            required
          />
        </label>
        {mutation.error && (
          <p className="error">{(mutation.error as Error).message}</p>
        )}
        <button type="submit" disabled={mutation.isPending}>
          {mutation.isPending ? "Signing in…" : "Sign in"}
        </button>
      </form>
    </div>
  );
}
