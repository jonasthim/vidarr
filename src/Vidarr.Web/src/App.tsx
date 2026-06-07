import { Navigate, Route, Routes } from "react-router-dom";
import { Layout } from "./components/Layout";
import { RequireAuth } from "./components/RequireAuth";
import { LibraryPage } from "./pages/Library";
import { AddArtistPage } from "./pages/AddArtist";
import { ActivityPage } from "./pages/Activity";
import { CalendarPlaceholder } from "./pages/CalendarPlaceholder";
import { WantedPlaceholder } from "./pages/WantedPlaceholder";
import { SettingsPage } from "./pages/Settings";
import { SystemPage } from "./pages/System";
import { LoginPage } from "./pages/Login";
import { ArtistDetailStub } from "./pages/ArtistDetailStub";

export function App(): JSX.Element {
  return (
    <Routes>
      <Route path="/login" element={<LoginPage />} />
      <Route element={<RequireAuth />}>
        <Route element={<Layout />}>
          <Route index element={<Navigate to="/library" replace />} />
          <Route path="library" element={<LibraryPage />} />
          <Route path="library/:artistId" element={<ArtistDetailStub />} />
          <Route path="add" element={<AddArtistPage />} />
          <Route path="calendar" element={<CalendarPlaceholder />} />
          <Route path="activity">
            <Route index element={<Navigate to="/activity/queue" replace />} />
            <Route path=":tab" element={<ActivityPage />} />
          </Route>
          <Route path="wanted" element={<WantedPlaceholder />} />
          <Route path="settings">
            <Route index element={<Navigate to="/settings/mediamanagement" replace />} />
            <Route path=":section" element={<SettingsPage />} />
          </Route>
          <Route path="system">
            <Route index element={<Navigate to="/system/status" replace />} />
            <Route path=":tab" element={<SystemPage />} />
          </Route>
          <Route path="*" element={<Navigate to="/library" replace />} />
        </Route>
      </Route>
    </Routes>
  );
}
