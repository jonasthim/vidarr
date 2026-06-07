/* Adapted from Sonarr (GPL-3.0). Vidarr branding; no signal-r/notifications. */
import { Link } from "react-router-dom";
import { FontAwesomeIcon } from "@fortawesome/react-fontawesome";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { icons } from "../../Icon/Icon";
import { api } from "../../../api";
import styles from "./PageHeader.module.css";

export function PageHeader(): JSX.Element {
  const queryClient = useQueryClient();
  const auth = useQuery({
    queryKey: ["auth-status"],
    queryFn: api.getAuthStatus,
    refetchInterval: false,
    retry: false,
  });
  const logout = useMutation({
    mutationFn: api.logout,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ["auth-status"] }),
  });
  const showLogout = auth.data?.enabled === true;

  return (
    <div className={styles.header}>
      <div className={styles.logoContainer}>
        <Link to="/library" className={styles.logo}>
          Vid<span>arr</span>
        </Link>
      </div>
      <div className={styles.right}>
        {showLogout && (
          <button
            type="button"
            className={styles.headerButton}
            onClick={() => logout.mutate()}
            title={auth.data?.username ?? "Sign out"}
          >
            <FontAwesomeIcon icon={icons.LOGOUT} /> Sign out
          </button>
        )}
      </div>
    </div>
  );
}
