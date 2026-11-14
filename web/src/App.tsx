import { BrowserRouter, Navigate, Route, Routes } from "react-router-dom";
import { AuthProvider } from "./shared/contexts/AuthContext";
import Login from "./features/onboarding/Login";
import ProfileSelect from "./features/onboarding/ProfileSelect";
import FamilyDashboard from "./features/family/FamilyDashboard";
import AxolotlChat from "./features/chat/AxolotlChat";
import CreateExplorer from "./features/onboarding/CreateExplorer";

export default function App() {
  return (
    <AuthProvider>
      <BrowserRouter>
        <Routes>
          <Route path="/" element={<Navigate to="/login" replace />} />
          <Route path="/login" element={<Login />} />
          <Route path="/profile-select" element={<ProfileSelect />} />
          <Route path="/family-dashboard" element={<FamilyDashboard />} />
          <Route path="/chat" element={<AxolotlChat />} />
          <Route path="/add-explorer" element={<CreateExplorer />} />
        </Routes>
      </BrowserRouter>
    </AuthProvider>
  );
}


