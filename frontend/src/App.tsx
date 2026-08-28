import { Navigate, Route, Routes } from 'react-router-dom'
import { AppShell, AuthenticatedGuard, RoleGuard } from './components'
import { AccountSecurityPage } from './pages/AccountPages'
import { ConfirmEmailPage, EntryLandingPage, ForgotPasswordPage, LoginPage, PortalUserRegisterPage, RegisterPage, RegistrationChoicePage, ResetPasswordPage } from './pages/PublicPages'
import { AssessmentPage, PlayerDashboard, ProfilePage, ProgressPage, TrainingPlanPage, WorkoutPage } from './pages/PlayerPages'
import { AdminAssessmentsPage, AdminDashboard, AdminExercisesPage, AdminMunicipalitiesPage, AdminProgramsPage, AdminUsersPage, AnalyticsPage, ChildDetailPage, CoachDashboard, CoachPlayerPage, CoachPlayersPage, ParentDashboard } from './pages/RolePages'
import { AdminSchoolsPage, SchoolCoachesPage, SchoolPlayersPage, SchoolSettingsPage } from './pages/SchoolPages'
import { SchoolDashboardPage, SchoolTeamsPage } from './pages/SchoolWorkspacePages'
import { TeamTrainingDetailPage, TeamTrainingJournalPage } from './pages/TeamTrainingPages'
import { CoachTeamsPage } from './pages/CoachTeamPage'
import { OrganizerActivitiesPage, OrganizerRegisterPage, PublicActivityPage, SportsNearbyPage } from './pages/SportsNearbyPages'

export default function App() {
  return <Routes>
    <Route path="/" element={<EntryLandingPage />} /><Route path="/sports" element={<SportsNearbyPage />} /><Route path="/activities/:slug" element={<PublicActivityPage />} /><Route path="/join" element={<RegistrationChoicePage />} /><Route path="/register-parent" element={<PortalUserRegisterPage role="Parent" />} /><Route path="/register-coach" element={<PortalUserRegisterPage role="Coach" />} /><Route path="/register-organizer" element={<OrganizerRegisterPage />} /><Route path="/login" element={<LoginPage />} /><Route path="/register" element={<RegisterPage />} /><Route path="/forgot-password" element={<ForgotPasswordPage />} /><Route path="/reset-password" element={<ResetPasswordPage />} /><Route path="/confirm-email" element={<ConfirmEmailPage />} />
    <Route element={<AuthenticatedGuard />}><Route path="/organizer/activities" element={<OrganizerActivitiesPage />} /><Route element={<AppShell />}><Route path="/account/security" element={<AccountSecurityPage />} /></Route></Route>
    <Route element={<RoleGuard role="Player" />}><Route element={<AppShell />}><Route path="/player" element={<PlayerDashboard />} /><Route path="/player/profile" element={<ProfilePage />} /><Route path="/player/assessment" element={<AssessmentPage />} /><Route path="/player/training" element={<TrainingPlanPage />} /><Route path="/player/training/:sessionId" element={<WorkoutPage />} /><Route path="/player/progress" element={<ProgressPage />} /></Route></Route>
    <Route element={<RoleGuard role="Coach" />}><Route element={<AppShell />}><Route path="/coach" element={<CoachDashboard />} /><Route path="/coach/teams" element={<CoachTeamsPage />} /><Route path="/coach/trainings" element={<TeamTrainingJournalPage />} /><Route path="/coach/trainings/:trainingId" element={<TeamTrainingDetailPage />} /><Route path="/coach/players" element={<CoachPlayersPage />} /><Route path="/coach/players/:playerId" element={<CoachPlayerPage />} /></Route></Route>
    <Route element={<RoleGuard role="Parent" />}><Route element={<AppShell />}><Route path="/parent" element={<ParentDashboard />} /><Route path="/parent/children/:playerId" element={<ChildDetailPage />} /></Route></Route>
    <Route element={<RoleGuard role="RegionalAnalyst" />}><Route element={<AppShell />}><Route path="/analytics" element={<AnalyticsPage />} /></Route></Route>
    <Route element={<RoleGuard role={["SchoolOwner", "SchoolAdmin"]} />}><Route element={<AppShell />}><Route path="/school" element={<SchoolDashboardPage />} /><Route path="/school/teams" element={<SchoolTeamsPage />} /><Route path="/school/coaches" element={<SchoolCoachesPage />} /><Route path="/school/players" element={<SchoolPlayersPage />} /><Route path="/school/settings" element={<SchoolSettingsPage />} /></Route></Route>
    <Route element={<RoleGuard role="Admin" />}><Route element={<AppShell />}><Route path="/admin" element={<AdminDashboard />} /><Route path="/admin/schools" element={<AdminSchoolsPage />} /><Route path="/admin/exercises" element={<AdminExercisesPage />} /><Route path="/admin/assessments" element={<AdminAssessmentsPage />} /><Route path="/admin/programs" element={<AdminProgramsPage />} /><Route path="/admin/municipalities" element={<AdminMunicipalitiesPage />} /><Route path="/admin/users" element={<AdminUsersPage />} /></Route></Route>
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes>
}
