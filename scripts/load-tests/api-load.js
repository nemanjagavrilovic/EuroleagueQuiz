import http from 'k6/http';
import { check, fail, sleep } from 'k6';
import { Counter, Rate, Trend } from 'k6/metrics';

const BASE = (__ENV.BASE_URL || 'http://localhost:5248/api').replace(/\/+$/, '');
const PROFILE = (__ENV.PROFILE || 'smoke').toLowerCase();
const WORKLOAD = (__ENV.WORKLOAD || 'read').toLowerCase();
const GAME = (__ENV.GAME || 'all').toLowerCase();
const LOCAL = /^https?:\/\/(localhost|127\.0\.0\.1|\[::1\]|host\.docker\.internal)(:\d+)?\/api$/i.test(BASE);
const PRODUCTION = BASE === 'https://pickandroll.io/api';
const WRITES = WORKLOAD === 'guest' || WORKLOAD === 'practice';
const TOKEN = __ENV.AUTH_TOKEN || '';
const PEAK = Number(__ENV.MAX_VUS || (PROFILE === 'stress' ? 50 : 20));
const THINK = Math.max(PROFILE === 'production-smoke' ? 1 : 0.1, Number(__ENV.THINK_SECONDS || 1));

if (!['smoke', 'load', 'stress', 'production-smoke'].includes(PROFILE)) throw new Error('Unknown PROFILE.');
if (!['read', 'guest', 'practice', 'admin'].includes(WORKLOAD)) throw new Error('Unknown WORKLOAD.');
if (!Number.isFinite(PEAK) || !Number.isInteger(PEAK) || PEAK < 1 || PEAK > 100) throw new Error('MAX_VUS must be between 1 and 100.');
if (!Number.isFinite(THINK)) throw new Error('THINK_SECONDS must be a number.');
if (!LOCAL && !(PRODUCTION && PROFILE === 'production-smoke' && WORKLOAD === 'read' && __ENV.ALLOW_PRODUCTION === 'true'))
  throw new Error('Remote traffic is restricted to explicitly enabled, read-only production-smoke on https://pickandroll.io/api.');
if (PROFILE === 'production-smoke' && (!PRODUCTION || WORKLOAD !== 'read' || __ENV.ALLOW_PRODUCTION !== 'true'))
  throw new Error('production-smoke requires the production URL, WORKLOAD=read and ALLOW_PRODUCTION=true.');
if (WRITES && (!LOCAL || __ENV.CONFIRM_TEST_DATABASE !== 'true'))
  throw new Error('Gameplay writes require a local API and CONFIRM_TEST_DATABASE=true. Verify its database is isolated first.');
if ((WORKLOAD === 'practice' || WORKLOAD === 'admin') && !TOKEN) throw new Error('AUTH_TOKEN is required. Sign in once before the run.');
if (__ENV.K6_VUS || __ENV.K6_DURATION || __ENV.K6_STAGES || __ENV.K6_ITERATIONS)
  throw new Error('Use the supplied PROFILE/MAX_VUS controls instead of K6_VUS, K6_DURATION, K6_STAGES or K6_ITERATIONS overrides.');

const allCases = [
  { game: 'grid', difficulty: 'Easy' }, { game: 'grid', difficulty: 'Normal' },
  { game: 'grid', difficulty: 'Hard' }, { game: 'grid', difficulty: 'Legend' },
  ...['team-history', 'missing-club', 'career-order', 'mystery-player', 'five-player'].map(game => ({ game }))
];
const cases = allCases.filter(item => GAME === 'all' || item.game === GAME);
if (!cases.length) throw new Error('Unknown GAME.');

const serverErrors = new Rate('server_error_rate');
const throttled = new Rate('throttled_rate');
const journeyFailures = new Rate('journey_failure_rate');
const starts = new Trend('game_start_ms', true);
const searches = new Trend('player_search_ms', true);
const finished = new Counter('completed_game_journeys');
let phase = 'load';

const scenario = PROFILE === 'smoke'
  ? { executor: 'shared-iterations', vus: 1, iterations: WRITES ? cases.length : 3, maxDuration: '5m' }
  : PROFILE === 'production-smoke'
    ? { executor: 'constant-vus', vus: 1, duration: '30s', gracefulStop: '10s' }
    : { executor: 'ramping-vus', startVUs: 0, gracefulRampDown: '15s', gracefulStop: '30s', stages: [
        { duration: '30s', target: Math.min(5, PEAK) },
        { duration: '45s', target: Math.min(5, PEAK) },
        { duration: '30s', target: Math.max(1, Math.ceil(PEAK / 2)) },
        { duration: '60s', target: Math.max(1, Math.ceil(PEAK / 2)) },
        { duration: '30s', target: PEAK },
        { duration: PROFILE === 'stress' ? '90s' : '60s', target: PEAK },
        { duration: '30s', target: 0 }
      ] };

export const options = {
  scenarios: { portal: scenario },
  setupTimeout: '60s',
  maxRedirects: 0,
  systemTags: ['status', 'method', 'name', 'group', 'check', 'error_code', 'scenario', 'expected_response'],
  thresholds: {
    'http_req_failed{phase:load}': [{ threshold: 'rate<0.01', abortOnFail: true, delayAbortEval: '15s' }],
    'server_error_rate{phase:load}': [{ threshold: 'rate<0.01', abortOnFail: true, delayAbortEval: '10s' }],
    'throttled_rate{phase:load}': ['rate<0.01'],
    'journey_failure_rate': ['rate<0.01'],
    'checks{phase:load}': ['rate>0.99'],
    ...(PROFILE === 'smoke' ? {} : {
      'http_req_duration{phase:load}': [`p(95)<${Number(__ENV.P95_MS || 3000)}`],
      ...(WORKLOAD === 'read' ? { 'player_search_ms{phase:load}': [`p(95)<${Number(__ENV.SEARCH_P95_MS || 1000)}`] } : {}),
      ...(WRITES ? { 'game_start_ms{phase:load}': [`p(95)<${Number(__ENV.START_P95_MS || 3000)}`] } : {})
    })
  }
};

function uuid() {
  return 'xxxxxxxx-xxxx-4xxx-yxxx-xxxxxxxxxxxx'.replace(/[xy]/g, character => {
    const value = Math.floor(Math.random() * 16);
    return (character === 'x' ? value : (value & 3) | 8).toString(16);
  });
}

function send(method, route, body, headers = {}, kind = 'read', game = 'none') {
  const tags = { name: route.split('?')[0].replace(/\/[0-9a-f-]{36}\//ig, '/{sessionId}/'), phase, kind, game };
  const response = http.request(method, BASE + route, body == null ? null : JSON.stringify(body), {
    headers: { Accept: 'application/json', 'Content-Type': 'application/json', ...headers }, tags, timeout: '30s'
  });
  serverErrors.add(response.status === 0 || response.status >= 500, tags);
  throttled.add(response.status === 429, tags);
  const ok = check(response, { [`${method} ${tags.name}: HTTP 2xx`]: value => value.status >= 200 && value.status < 300 }, tags);
  if (kind === 'start') starts.add(response.timings.duration, tags);
  if (kind === 'search') searches.add(response.timings.duration, tags);
  if (!ok) return null;
  if (response.status === 204) return {};
  try { return response.json(); }
  catch { check(false, { 'API response is JSON': value => value }, tags); return null; }
}

function pause() { sleep(THINK); }
function isFinished(value) { return !!value && (value.completed === true || value.round?.completed === true || value.gameCompleted === true || value.round?.gameCompleted === true); }

export function setup() {
  phase = 'preflight';
  if (!send('GET', '/config/public')) fail('API preflight failed. Check the URL, API logs and local database settings.');
  const teams = send('GET', '/teams');
  if (!Array.isArray(teams) || teams.length < 6) fail('Copy/import the basketball catalog into the test database before load testing.');
  if (WORKLOAD === 'practice' || WORKLOAD === 'admin') {
    const user = send('GET', '/auth/me', null, { Authorization: `Bearer ${TOKEN}` });
    if (!user) fail('AUTH_TOKEN is invalid or expired. Sign in again before the test.');
    if (WORKLOAD === 'admin' && !user.roles?.includes('Admin')) fail('The admin workload requires an Admin token.');
  }
  phase = 'load';
}

function readJourney() {
  let ok = Array.isArray(send('GET', '/teams'));
  pause();
  const terms = ['James', 'Jovic', 'Micic', 'Nando', 'Mirotic'];
  ok = Array.isArray(send('GET', `/players/search?term=${terms[(__VU + __ITER) % terms.length]}`, null, {}, 'search')) && ok;
  pause();
  ok = !!send('GET', '/leaderboard?period=today&game=all&page=1&pageSize=20') && ok;
  pause();
  return ok;
}

function guestJourney(item) {
  const guestId = 'c0dec0de-' + uuid().slice(9);
  const headers = { 'X-Guest-Id': guestId };
  const prefix = item.game === 'grid' ? '/grid/daily'
    : item.game === 'team-history' ? '/team-history/daily'
    : item.game === 'five-player' ? '/five-player/daily' : `/career-games/${item.game}/daily`;
  const startRoute = prefix + '/start?ranked=false' + (item.difficulty ? `&difficulty=${item.difficulty}` : '');
  let result = send('POST', startRoute, {}, headers, 'start', item.game);
  if (!result) return false;
  const challengeId = result.challengeId;
  if (!Number.isInteger(challengeId)) return false;
  pause();
  if (item.game === 'grid') result = send('POST', prefix + '/reveal?ranked=false', { challengeId }, headers, 'finish', item.game);
  else if (item.game === 'career-order') result = send('POST', prefix + '/submit?ranked=false', {
    challengeId, commandId: uuid(), teamIds: result.round.clubs.map(club => club.id)
  }, headers, 'finish', item.game);
  else if (item.game === 'five-player') {
    for (let round = 1; round <= 5 && result; round++) {
      result = send('POST', prefix + '/guess?ranked=false', { challengeId, commandId: uuid(), roundNumber: round, skip: true }, headers, 'finish', item.game);
      pause();
      if (round < 5 && result) result = send('POST', prefix + '/next?ranked=false', { challengeId, commandId: uuid(), roundNumber: round }, headers, 'next', item.game);
    }
  } else {
    for (let clue = 0; clue < 6 && result && !result.round.completed; clue++) {
      result = send('POST', prefix + '/guess?ranked=false', { challengeId, commandId: uuid(), skip: true }, headers, 'finish', item.game);
      pause();
    }
  }
  const completed = item.game === 'five-player' ? result?.round?.gameCompleted === true : isFinished(result);
  // Resuming the same browser/day must not create another attempt or audit start.
  const resumed = completed ? send('POST', startRoute, {}, headers, 'resume', item.game) : null;
  return completed && resumed?.challengeId === challengeId &&
    (item.game === 'five-player' ? resumed?.round?.gameCompleted === true : isFinished(resumed));
}

function practiceJourney(item) {
  const headers = { Authorization: `Bearer ${TOKEN}` };
  const prefix = item.game === 'grid' ? '/grid'
    : item.game === 'team-history' ? '/team-history'
    : item.game === 'five-player' ? '/five-player' : `/career-games/${item.game}`;
  let result = send('POST', item.game === 'grid' ? `/grid/change?difficulty=${item.difficulty}` : prefix + '/start', {}, headers, 'start', item.game);
  if (!result) return false;
  pause();
  if (item.game === 'grid') {
    if (!result.practiceSessionId) return false;
    const revealed = send('POST', '/grid/reveal', { rowTeamIds: result.rowTeams.map(club => club.id),
      columnTeamIds: result.columnTeams.map(club => club.id), practiceSessionId: result.practiceSessionId }, headers, 'finish', item.game);
    return Array.isArray(revealed?.cells) && revealed.cells.length === 9;
  }
  const roundId = result.roundId;
  if (item.game === 'career-order') result = send('POST', prefix + '/guess', { roundId, teamIds: result.clubs.map(club => club.id) }, headers, 'finish', item.game);
  else if (item.game === 'five-player') {
    for (let round = 1; round <= 5 && result; round++) {
      result = send('POST', prefix + '/guess', { roundId, skip: true }, headers, 'finish', item.game);
      pause();
      if (round < 5 && result) result = send('POST', prefix + `/next?roundId=${roundId}`, {}, headers, 'next', item.game);
    }
  } else {
    for (let clue = 0; clue < 6 && result && !result.completed; clue++) {
      result = send('POST', prefix + '/guess', { roundId, skip: true }, headers, 'finish', item.game);
      pause();
    }
  }
  // Starting Five must finish all five rounds, not merely the first round.
  return item.game === 'five-player' ? result?.gameCompleted === true : result?.completed === true;
}

export default function runScenario() {
  const item = cases[(__VU + __ITER - 1) % cases.length];
  let ok = false;
  try {
    if (WORKLOAD === 'read') ok = readJourney();
    else if (WORKLOAD === 'admin') {
      const headers = { Authorization: `Bearer ${TOKEN}` };
      ok = !!send('GET', '/admin/audit-logs?page=1&pageSize=25', null, headers);
      pause();
      ok = !!send('GET', '/admin/audit-logs/guest-activity', null, headers) && ok;
      // Also exercise the registered-player search subquery.
      pause();
      ok = !!send('GET', '/admin/audit-logs?actorType=User&search=Game.&page=1&pageSize=25', null, headers) && ok;
    } else ok = WORKLOAD === 'guest' ? guestJourney(item) : practiceJourney(item);
  } catch { ok = false; }
  check(ok, { 'journey finished with the expected API state': value => value }, { phase, game: item.game });
  journeyFailures.add(!ok);
  if (ok && WRITES) finished.add(1, { game: item.game, difficulty: item.difficulty || 'none' });
  pause();
}

export function handleSummary(data) {
  const thresholds = Object.entries(data.metrics).flatMap(([metric, value]) =>
    Object.entries(value.thresholds || {}).map(([rule, outcome]) => ({ metric, rule, passed: outcome.ok })));
  const overview = { target: BASE, profile: PROFILE, workload: WORKLOAD, game: GAME,
    requests: data.metrics.http_reqs?.values,
    requestDuration: data.metrics.http_req_duration?.values,
    gameStart: data.metrics.game_start_ms?.values,
    playerSearch: data.metrics.player_search_ms?.values,
    serverErrors: data.metrics.server_error_rate?.values,
    throttled: data.metrics.throttled_rate?.values,
    journeyFailures: data.metrics.journey_failure_rate?.values,
    completedJourneys: data.metrics.completed_game_journeys?.values,
    checks: data.metrics.checks?.values, thresholds };
  return { stdout: JSON.stringify(overview, null, 2) + '\n',
    [__ENV.RESULTS_FILE || 'load-test-summary.json']: JSON.stringify({ overview, results: data }, null, 2) };
}
