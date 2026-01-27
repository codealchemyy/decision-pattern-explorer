import { Link, Route, Routes } from "react-router-dom";
import LoginPage from "./pages/LoginPage";
import DashboardPage from "./pages/DashboardPage";
import NewDecisionPage from "./pages/NewDecisionPage";
import DecisionDetailPage from "./pages/DecisionDetailPage";
import CommunityPage from "./pages/CommunityPage";
import PostDetailPage from "./pages/PostDetailPage";

export default function App() {
  return (
    <div style={{ padding: 24 }}>
      <h1>Decision Pattern Explorer</h1>

      <nav style={{ display: "flex", gap: 12, marginBottom: 24 }}>
        <Link to="/login">Login</Link>
        <Link to="/dashboard">Dashboard</Link>
        <Link to="/decisions/new">New decision</Link>
        <Link to="/decisions/1">Decision #1</Link>
        <Link to="/community">Community</Link>
        <Link to="/community/posts/1">Post #1</Link>
      </nav>

      <Routes>
        <Route path="/login" element={<LoginPage />} />
        <Route path="/dashboard" element={<DashboardPage />} />
        <Route path="/decisions/new" element={<NewDecisionPage />} />
        <Route path="/decisions/:id" element={<DecisionDetailPage />} />
        <Route path="/community" element={<CommunityPage />} />
        <Route path="/community/posts/:postId" element={<PostDetailPage />} />

        <Route path="*" element={<DashboardPage />} />
      </Routes>
    </div>
  );
}