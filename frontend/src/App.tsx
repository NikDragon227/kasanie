import { useEffect } from 'react'
import { Navigate, Route, Routes, useLocation } from 'react-router-dom'
import { CoachScopeProvider } from './CoachScope'
import { useAuth } from './auth'
import { AppShell, AuthenticatedGuard, RoleGuard } from './components'
import { AccountSecurityPage } from './pages/AccountPages'
import { ConfirmEmailPage, ForgotPasswordPage, LoginPage, PortalUserRegisterPage, RegisterPage, RegistrationChoicePage, ResetPasswordPage, ResendConfirmationPage } from './pages/PublicPages'
import { OrganizerRulesPage, PrivacyPolicyPage, TermsPage } from './pages/LegalPages'
import { AssessmentPage, PlayerDashboard, ProfilePage, ProgressPage, TrainingPlanPage, WorkoutPage } from './pages/PlayerPages'
import { AdminAssessmentsPage, AdminDashboard, AdminExercisesPage, AdminFeedbackPage, AdminMunicipalitiesPage, AdminProgramsPage, AdminUsersPage, ChildDetailPage, CoachDashboard, CoachPlayerPage, CoachPlayersPage, ParentDashboard } from './pages/RolePages'
import { AdminSchoolsPage, SchoolCoachesPage, SchoolPlayersPage, SchoolSettingsPage } from './pages/SchoolPages'
import { SchoolDashboardPage, SchoolTeamsPage } from './pages/SchoolWorkspacePages'
import { TeamTrainingDetailPage, TeamTrainingJournalPage } from './pages/TeamTrainingPages'
import { TrainingWorkspacePage } from './pages/TrainingWorkspace'
import { CoachTeamsPage } from './pages/CoachTeamPage'
import { GoalkeeperCabinetPage } from './pages/GoalkeeperCabinet'
import { GuestParticipationPage, MyActivitiesPage, OrganizerActivitiesPage, OrganizerRegisterPage, PublicActivityPage, SportsNearbyPage } from './pages/SportsNearbyPages'
import { FeedbackWidget } from './FeedbackWidget'
import { initYandexMetrica, trackYandexMetricaPageView } from './metrica'

function CoachShell() {
  const { user } = useAuth()
  return <CoachScopeProvider key={user?.id} userId={user?.id ?? ''}><AppShell /></CoachScopeProvider>
}

function SportsRouteRedirect() {
  const location = useLocation()
  return <Navigate to={{ pathname: '/', search: location.search, hash: location.hash }} replace />
}

function MetricaTracker() {
  const location = useLocation()
  useEffect(() => {
    initYandexMetrica()
    trackYandexMetricaPageView(location.pathname)
  }, [location.pathname])
  return null
}

export default function App() {
  return <><MetricaTracker /><Routes>
    <Route element={<RoleGuard role="Coach" />}><Route element={<AppShell />}><Route path="/goalkeeper" element={<GoalkeeperCabinetPage />} /></Route></Route>
    <Route path="/" element={<SportsNearbyPage />} /><Route path="/sports" element={<SportsRouteRedirect />} /><Route path="/activities/:slug" element={<PublicActivityPage />} /><Route path="/guest/participations/:token" element={<GuestParticipationPage />} /><Route path="/join" element={<RegistrationChoicePage />} /><Route path="/register-parent" element={<PortalUserRegisterPage role="Parent" />} /><Route path="/register-coach" element={<PortalUserRegisterPage role="Coach" />} /><Route path="/register-organizer" element={<OrganizerRegisterPage />} /><Route path="/login" element={<LoginPage />} /><Route path="/register" element={<RegisterPage />} /><Route path="/forgot-password" element={<ForgotPasswordPage />} /><Route path="/reset-password" element={<ResetPasswordPage />} /><Route path="/confirm-email" element={<ConfirmEmailPage />} /><Route path="/resend-confirmation" element={<ResendConfirmationPage />} /><Route path="/documents/privacy" element={<PrivacyPolicyPage />} /><Route path="/documents/terms" element={<TermsPage />} /><Route path="/documents/organizer-rules" element={<OrganizerRulesPage />} />
    <Route element={<AuthenticatedGuard />}><Route path="/my/activities" element={<MyActivitiesPage />} /><Route path="/organizer/activities" element={<OrganizerActivitiesPage />} /><Route element={<AppShell />}><Route path="/account/security" element={<AccountSecurityPage />} /></Route></Route>
    <Route element={<RoleGuard role="Player" />}><Route element={<AppShell />}><Route path="/player" element={<PlayerDashboard />} /><Route path="/player/profile" element={<ProfilePage />} /><Route path="/player/assessment" element={<AssessmentPage />} /><Route path="/player/training" element={<TrainingPlanPage />} /><Route path="/player/training/:sessionId" element={<WorkoutPage />} /><Route path="/player/progress" element={<ProgressPage />} /></Route></Route>
    <Route element={<RoleGuard role="Coach" />}><Route element={<CoachShell />}><Route path="/coach" element={<CoachDashboard />} /><Route path="/coach/teams" element={<CoachTeamsPage />} /><Route path="/coach/trainings" element={<TrainingWorkspacePage />} /><Route path="/coach/trainings/journal" element={<TeamTrainingJournalPage />} /><Route path="/coach/trainings/:trainingId" element={<TeamTrainingDetailPage />} /><Route path="/coach/players" element={<CoachPlayersPage />} /><Route path="/coach/players/:playerId" element={<CoachPlayerPage />} /></Route></Route>
    <Route element={<RoleGuard role="Parent" />}><Route element={<AppShell />}><Route path="/parent" element={<ParentDashboard />} /><Route path="/parent/children/:playerId" element={<ChildDetailPage />} /></Route></Route>
    <Route element={<RoleGuard role={["SchoolOwner", "SchoolAdmin", "Coach", "Admin"]} />}><Route element={<AppShell />}><Route path="/school" element={<SchoolDashboardPage />} /><Route path="/school/teams" element={<SchoolTeamsPage />} /><Route path="/school/coaches" element={<SchoolCoachesPage />} /><Route path="/school/players" element={<SchoolPlayersPage />} /><Route path="/school/settings" element={<SchoolSettingsPage />} /></Route></Route>
    <Route element={<RoleGuard role="Admin" />}><Route element={<AppShell />}><Route path="/admin" element={<AdminDashboard />} /><Route path="/admin/feedback" element={<AdminFeedbackPage />} /><Route path="/admin/schools" element={<AdminSchoolsPage />} /><Route path="/admin/exercises" element={<AdminExercisesPage />} /><Route path="/admin/assessments" element={<AdminAssessmentsPage />} /><Route path="/admin/programs" element={<AdminProgramsPage />} /><Route path="/admin/municipalities" element={<AdminMunicipalitiesPage />} /><Route path="/admin/users" element={<AdminUsersPage />} /></Route></Route>
    <Route path="*" element={<Navigate to="/" replace />} />
  </Routes><FeedbackWidget /></>
}
