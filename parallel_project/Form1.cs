using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace parallel_project
{
    public partial class Form1 : Form
    {
        private readonly MatchmakingManager _matchmaking = new MatchmakingManager();
        private readonly ArenaLoader _loader = new ArenaLoader();
        private readonly CombatEngine _combat = new CombatEngine();
        private readonly Random _rng = new Random();

        // Activity 1
        private GameConnection? _conn;
        private CancellationTokenSource? _matchCts;
        private CancellationTokenSource? _sessionCts;
        private CancellationTokenSource? _readyWindowCts;

        private bool _isHost;
        private string _localSlot = ""; // "P1" or "P2"

        // Activity 2
        private Player? _p1;
        private Player? _p2;
        private bool _localTeamReady;
        private bool _remoteTeamReady;
        private bool _localStartClicked;
        private bool _remoteStartClicked;
        private bool _fightCountdownStarted;

        // Selection (attacker + target)
        private bool _attackerSelected;
        private bool _targetSelected;
        private HeroKind _selectedAttackerKind = HeroKind.Warrior;
        private HeroKind _selectedTargetKind = HeroKind.Warrior;

        // Host-only: remote winner's chosen attacker/target
        private HeroKind _remoteChosenAttackerKind = HeroKind.Warrior;
        private HeroKind _remoteChosenTargetKind = HeroKind.Warrior;
        private bool _remoteChoiceReceived;

        // Activity 3
        private int _roundId;
        private int _requiredClicks;
        private int _clickCount;

        private TaskCompletionSource<bool>? _localClickDoneTcs;
        private TaskCompletionSource<string>? _remoteWinnerTcs;

        // Host-only: set when remote sends RACE_DONE for current round.
        private TaskCompletionSource<bool>? _remoteRaceDoneTcs;

        // Animation
        private readonly SemaphoreSlim _animLock = new SemaphoreSlim(1, 1);
        private readonly Dictionary<PictureBox, Point> _startPos = new Dictionary<PictureBox, Point>();

        private const int CancelWindowSeconds = 3;
        private const int RequiredClicksDefault = 12;
        private const int RaceTimeoutMs = 9000;

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            EnsurePixelSprites();
            CacheStartPositions();
            ResetUiToIdle();
        }

        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            await CleanupAsync("Form closing");
        }

        // -------------------- UI Events (Designer Wired) --------------------

        private async void btnHostGame_Click(object sender, EventArgs e)
        {
            await HostGameAsync();
        }

        private async void btnJoinGame_Click(object sender, EventArgs e)
        {
            await JoinGameAsync();
        }

        private void btnCancelSearch_Click(object sender, EventArgs e)
        {
            CancelSearch();
        }

        private async void btnLoadTeam_Click(object sender, EventArgs e)
        {
            await LoadTeamAsync();
        }

        private async void btnStartFight_Click(object sender, EventArgs e)
        {
            await StartFightClickedAsync();
        }

        private void btnAttackRace_Click(object sender, EventArgs e)
        {
            AttackRaceClick();
        }

        private void picTopWarrior_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Warrior);
        private void picTopMage_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Mage);
        private void picTopArcher_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Archer);
        private void picBottomWarrior_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Warrior);
        private void picBottomMage_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Mage);
        private void picBottomArcher_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Archer);

        private void OnHeroPictureClicked(string slot, HeroKind kind)
        {
            if (_localSlot != "P1" && _localSlot != "P2")
                return;

            if (!IsHeroAlive(slot, kind))
            {
                Log("Selection ignored: " + slot + " " + kind + " is dead.");
                return;
            }

            if (slot == _localSlot)
            {
                _selectedAttackerKind = kind;
                _attackerSelected = true;
                Log("Selected attacker: " + _localSlot + " " + kind);
            }
            else
            {
                _selectedTargetKind = kind;
                _targetSelected = true;
                Log("Selected target: " + slot + " " + kind);
            }

            UpdateSelectionHighlights();
        }

        // -------------------- Activity 1: Matchmaking --------------------

        private async Task HostGameAsync()
        {
            await CleanupAsync("Start host");

            _isHost = true;
            _localSlot = "P1";
            _p1 = new Player("Player 1", "P1");
            _p2 = new Player("Player 2", "P2");
            ResetRoundState();
            ResetTeamState();

            Log("Activity 1: Host clicked. Starting server...");
            SetRoleLabels();

            _matchCts = new CancellationTokenSource();
            _sessionCts = new CancellationTokenSource();

            btnHostGame.Enabled = false;
            btnJoinGame.Enabled = false;
            btnCancelSearch.Enabled = true;
            btnLoadTeam.Enabled = false;
            btnStartFight.Enabled = false;
            btnAttackRace.Enabled = false;

            lblStatus.Text = "Hosting... waiting for Player 2";

            try
            {
                Log("Activity 1: CancellationTokenSource created for matchmaking.");
                _conn = await _matchmaking.HostAsync(_matchCts.Token);
                HookConnection(_conn);

                Log("Connected: Player 2 joined.");
                lblStatus.Text = "Connected. Cancel window...";

                await _conn.SendAsync("ROLE|P2", _sessionCts.Token);
                await BeginCancelWindowAsync(CancelWindowSeconds);
            }
            catch (OperationCanceledException)
            {
                Log("Matchmaking cancelled (host).");
                await CleanupAsync("Host cancelled");
            }
            catch (Exception ex)
            {
                Log("Host error: " + ex.Message);
                await CleanupAsync("Host error");
            }
        }

        private async Task JoinGameAsync()
        {
            await CleanupAsync("Start join");

            _isHost = false;
            _localSlot = "";
            _p1 = new Player("Player 1", "P1");
            _p2 = new Player("Player 2", "P2");
            ResetRoundState();
            ResetTeamState();

            Log("Activity 1: Join clicked. Connecting...");
            SetRoleLabels();

            _matchCts = new CancellationTokenSource();
            _sessionCts = new CancellationTokenSource();

            btnHostGame.Enabled = false;
            btnJoinGame.Enabled = false;
            btnCancelSearch.Enabled = true;
            btnLoadTeam.Enabled = false;
            btnStartFight.Enabled = false;
            btnAttackRace.Enabled = false;

            lblStatus.Text = "Joining... connecting to host";

            try
            {
                Log("Activity 1: CancellationTokenSource created for matchmaking.");
                _conn = await _matchmaking.JoinAsync(_matchCts.Token);
                HookConnection(_conn);

                Log("Connected: Joined host.");
                lblStatus.Text = "Connected. Waiting for cancel window...";
            }
            catch (OperationCanceledException)
            {
                Log("Matchmaking cancelled (join).");
                await CleanupAsync("Join cancelled");
            }
            catch (Exception ex)
            {
                Log("Join error: " + ex.Message);
                await CleanupAsync("Join error");
            }
        }

        private void CancelSearch()
        {
            Log("Activity 1: Cancel requested -> CTS.Cancel()");
            lblStatus.Text = "Cancelling...";

            try { _readyWindowCts?.Cancel(); } catch { }
            try { _matchCts?.Cancel(); } catch { }

            if (_conn != null && _conn.IsConnected && _sessionCts != null)
                _ = _conn.SendAsync("CANCEL|user", _sessionCts.Token);

            _ = CleanupAsync("Cancelled");
        }

        private void HookConnection(GameConnection conn)
        {
            conn.MessageReceived += OnNetMessage;
            conn.Disconnected += (reason) =>
            {
                Log("Disconnected: " + reason);
                _ = CleanupAsync("Disconnected");
            };
        }

        private async Task BeginCancelWindowAsync(int seconds)
        {
            if (_conn == null || _sessionCts == null)
                return;

            _readyWindowCts?.Cancel();
            _readyWindowCts = new CancellationTokenSource();
            CancellationToken token = _readyWindowCts.Token;

            if (_isHost)
                await _conn.SendAsync("READYWIN|" + seconds, _sessionCts.Token);

            Log("Activity 1: Cancel window started (" + seconds + " seconds)." );
            btnCancelSearch.Enabled = true;

            try
            {
                for (int i = seconds; i >= 1; i--)
                {
                    token.ThrowIfCancellationRequested();
                    lblCountdown.Text = i.ToString();
                    await Task.Delay(1000, token);
                }

                lblCountdown.Text = "";
                btnCancelSearch.Enabled = false;

                if (_isHost)
                    await _conn.SendAsync("UNLOCK_LOAD|", _sessionCts.Token);

                UnlockTeamLoading();
            }
            catch (OperationCanceledException)
            {
            }
        }

        private void UnlockTeamLoading()
        {
            Log("Match confirmed. Load Team unlocked.");
            lblStatus.Text = "Connected. Load your team.";
            btnLoadTeam.Enabled = true;
        }

        // -------------------- Activity 2: Team + Start Fight --------------------

        private async Task LoadTeamAsync()
        {
            if (_conn == null || !_conn.IsConnected || _sessionCts == null)
            {
                Log("Not connected.");
                return;
            }

            if (_localSlot != "P1" && _localSlot != "P2")
            {
                Log("Role not assigned yet.");
                return;
            }

            if (_localTeamReady)
            {
                Log("Local team already ready.");
                return;
            }

            btnLoadTeam.Enabled = false;
            lblStatus.Text = "Loading heroes...";

            Log("Activity 2: CountdownEvent initialized to 3." );
            Log("Activity 2: Loading Warrior/Mage/Archer on separate Tasks..." );

            try
            {
                Player team = await _loader.LoadLocalTeamAsync(
                    localName: _localSlot == "P1" ? "Player 1" : "Player 2",
                    localSlot: _localSlot,
                    log: Log,
                    token: _sessionCts.Token);

                ApplyTeam(team);
                _localTeamReady = true;
                UpdateAllUi();
                EnsureSelectionDefaults();
                UpdateSelectionHighlights();

                await _conn.SendAsync("TEAM_READY|" + team.Slot + "|" + SerializeTeam(team), _sessionCts.Token);
                Log("Activity 2: TEAM_READY sent to other player." );

                if (_remoteTeamReady)
                    OnBothTeamsReady();
                else
                    lblStatus.Text = "Team ready. Waiting for other player...";
            }
            catch (OperationCanceledException)
            {
                Log("Team load cancelled.");
                btnLoadTeam.Enabled = true;
            }
            catch (Exception ex)
            {
                Log("Team load error: " + ex.Message);
                btnLoadTeam.Enabled = true;
            }
        }

        private void OnBothTeamsReady()
        {
            Log("Both teams ready." );
            lblStatus.Text = "Both teams ready. Click Start Fight.";
            btnStartFight.Enabled = true;
        }

        private async Task StartFightClickedAsync()
        {
            if (_conn == null || !_conn.IsConnected || _sessionCts == null)
                return;

            if (!_localTeamReady || !_remoteTeamReady)
            {
                Log("Cannot start: both teams must be ready." );
                return;
            }

            if (_localStartClicked)
            {
                Log("Start Fight already clicked." );
                return;
            }

            _localStartClicked = true;
            btnStartFight.Enabled = false;
            Log("Activity 2: Start Fight clicked -> sending START_READY." );
            await _conn.SendAsync("START_READY|" + _localSlot, _sessionCts.Token);

            if (_isHost && _remoteStartClicked)
                await StartFightCountdownHostAsync();
            else
                lblStatus.Text = "Waiting for other player to click Start Fight...";
        }

        private async Task StartFightCountdownHostAsync()
        {
            if (_conn == null || _sessionCts == null)
                return;

            if (_fightCountdownStarted)
                return;
            _fightCountdownStarted = true;

            Log("Fight starting (both clicked Start Fight)." );
            btnAttackRace.Enabled = false;

            for (int i = 3; i >= 1; i--)
            {
                await BroadcastAsync("FIGHT_CD|" + i);
                await Task.Delay(700, _sessionCts.Token);
            }

            await BroadcastAsync("FIGHT_CD|FIGHT");
            await Task.Delay(700, _sessionCts.Token);
            await BroadcastAsync("FIGHT_CD|");

            _roundId = 0;
            await RunRoundHostAsync();
        }

        // -------------------- Activity 3: Interactive Speed Race --------------------

        private void AttackRaceClick()
        {
            if (_roundId <= 0 && _requiredClicks == 0)
                return;

            if (!btnAttackRace.Enabled)
                return;

            _clickCount++;
            lblStatus.Text = "Speed race: " + _clickCount + "/" + _requiredClicks;

            if (_clickCount >= _requiredClicks)
            {
                if (_localClickDoneTcs != null && _localClickDoneTcs.TrySetResult(true))
                {
                    btnAttackRace.Enabled = false;
                    Log("Speed race: local clicks complete." );

                    if (_conn != null && _conn.IsConnected && _sessionCts != null)
                    {
                        // Only the joiner must report completion to host.
                        if (!_isHost)
                        {
                            EnsureSelectionDefaults();
                            _ = _conn.SendAsync("RACE_DONE|" + _roundId + "|" + (int)_selectedAttackerKind + "|" + (int)_selectedTargetKind, _sessionCts.Token);
                        }
                    }
                }
            }
        }

        private async Task RunRoundHostAsync()
        {
            if (!_isHost || _conn == null || _sessionCts == null)
                return;
            if (_p1 == null || _p2 == null)
                return;

            if (!_p1.HasAliveHeroes() || !_p2.HasAliveHeroes())
            {
                await BroadcastAsync("GAME_OVER|" + (_p1.HasAliveHeroes() ? "P1" : "P2"));
                return;
            }

            _roundId++;
            _requiredClicks = _rng.Next(10, 16);
            _clickCount = 0;

            EnsureSelectionDefaults();
            UpdateSelectionHighlights();

            _localClickDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remoteWinnerTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remoteRaceDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remoteChoiceReceived = false;

            Log("Activity 3: ROUND_START broadcast. Required clicks: " + _requiredClicks);
            await BroadcastAsync("ROUND_START|" + _roundId + "|" + _requiredClicks + "|" + RaceTimeoutMs);

            Log("Activity 3: Task.WhenAny (host) waiting for local vs remote vs timeout...");
            Task finished = await Task.WhenAny(_localClickDoneTcs.Task, _remoteRaceDoneTcs.Task, Task.Delay(RaceTimeoutMs, _sessionCts.Token));

            string winnerSlot;
            if (finished == _remoteRaceDoneTcs.Task)
                winnerSlot = "P2";
            else
                winnerSlot = "P1";

            Log("Speed race winner: " + winnerSlot);
            await BroadcastAsync("ROUND_WINNER|" + _roundId + "|" + winnerSlot);

            string attackerSlot;
            string defenderSlot;
            HeroKind attackerKind;
            HeroKind defenderKind;

            if (winnerSlot == "P1")
            {
                attackerSlot = "P1";
                defenderSlot = "P2";
                attackerKind = _selectedAttackerKind;
                defenderKind = _selectedTargetKind;
            }
            else
            {
                attackerSlot = "P2";
                defenderSlot = "P1";

                // If remote didn't send choices (shouldn't happen), use fallbacks.
                attackerKind = _remoteChoiceReceived ? _remoteChosenAttackerKind : HeroKind.Warrior;
                defenderKind = _remoteChoiceReceived ? _remoteChosenTargetKind : HeroKind.Warrior;
            }

            CombatEngine.AttackResult atk = await _combat.ResolveAttackAsync(_p1, _p2, attackerSlot, attackerKind, defenderSlot, defenderKind);
            string attackWire = "ATTACK|" + _roundId + "|" + attackerSlot + "|" + (int)attackerKind + "|" + defenderSlot + "|" + (int)defenderKind + "|" + atk.Damage + "|" + atk.DefenderHpAfter + "|" + (atk.DefenderDied ? "1" : "0");
            await BroadcastAsync(attackWire);

            if (atk.GameOver)
            {
                await BroadcastAsync("GAME_OVER|" + atk.GameWinnerSlot);
                return;
            }

            await Task.Delay(700, _sessionCts.Token);
            await RunRoundHostAsync();
        }

        private void EnableRaceUi()
        {
            btnAttackRace.Enabled = true;
            lblStatus.Text = "Speed race: click " + _requiredClicks + " times!";
        }

        private async Task RunLocalRaceWatcherAsync(int roundId, int timeoutMs, CancellationToken token)
        {
            try
            {
                Log("Activity 3: Task.WhenAny (local) started." );
                Task timeout = Task.Delay(timeoutMs, token);
                Task finished = await Task.WhenAny(_localClickDoneTcs!.Task, _remoteWinnerTcs!.Task, timeout);

                if (finished == timeout)
                {
                    Log("Speed race timeout (waiting for winner)..." );
                    btnAttackRace.Enabled = false;
                    await _remoteWinnerTcs.Task;
                    return;
                }

                if (finished == _remoteWinnerTcs.Task)
                {
                    btnAttackRace.Enabled = false;
                    return;
                }

                // Local completed first; we already sent RACE_DONE in the click handler.
            }
            catch (OperationCanceledException)
            {
            }
            catch (Exception ex)
            {
                Log("Local race watcher error: " + ex.Message);
            }
        }

        // -------------------- Networking --------------------

        private async Task BroadcastAsync(string msg)
        {
            if (_conn == null || !_conn.IsConnected || _sessionCts == null)
                return;

            await _conn.SendAsync(msg, _sessionCts.Token);
            OnNetMessage(msg); // host also applies locally
        }

        private void OnNetMessage(string raw)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => OnNetMessage(raw)));
                return;
            }

            int idx = raw.IndexOf('|');
            string type = idx < 0 ? raw : raw.Substring(0, idx);
            string payload = idx < 0 ? "" : raw.Substring(idx + 1);

            switch (type)
            {
                case "ROLE":
                    _localSlot = payload.Trim();
                    Log("Role assigned: " + _localSlot);
                    SetRoleLabels();
                    break;

                case "READYWIN":
                    if (int.TryParse(payload, out int sec))
                        _ = BeginCancelWindowAsync(sec);
                    break;

                case "UNLOCK_LOAD":
                    UnlockTeamLoading();
                    break;

                case "CANCEL":
                    Log("Cancelled by other player." );
                    _ = CleanupAsync("Cancelled by remote");
                    break;

                case "TEAM_READY":
                    HandleTeamReady(payload);
                    break;

                case "START_READY":
                    HandleStartReady(payload);
                    break;

                case "FIGHT_CD":
                    HandleFightCountdown(payload);
                    break;

                case "ROUND_START":
                    HandleRoundStart(payload);
                    break;

                case "RACE_DONE":
                    HandleRaceDone(payload);
                    break;

                case "ROUND_WINNER":
                    HandleRoundWinner(payload);
                    break;

                case "ATTACK":
                    HandleAttack(payload);
                    break;

                case "GAME_OVER":
                    HandleGameOver(payload);
                    break;
            }
        }

        private void HandleTeamReady(string payload)
        {
            // TEAM_READY|<slot>|<hero;hero;hero>
            string[] parts = payload.Split('|');
            if (parts.Length < 2) return;

            string slot = parts[0];
            string teamWire = parts[1];

            Player p = new Player(slot == "P1" ? "Player 1" : "Player 2", slot);
            p.Heroes = DeserializeTeam(teamWire);
            ApplyTeam(p);

            if (slot != _localSlot)
                _remoteTeamReady = true;
            else
                _localTeamReady = true;

            Log("Team ready received: " + slot);
            UpdateAllUi();
            EnsureSelectionDefaults();
            UpdateSelectionHighlights();

            if (_localTeamReady && _remoteTeamReady)
                OnBothTeamsReady();
        }

        private void HandleStartReady(string payload)
        {
            _remoteStartClicked = true;
            Log("Activity 2: Other player clicked Start Fight." );

            if (_localStartClicked && _remoteStartClicked)
            {
                lblStatus.Text = "Both clicked Start Fight.";
                if (_isHost)
                    _ = StartFightCountdownHostAsync();
            }
        }

        private void HandleFightCountdown(string payload)
        {
            lblCountdown.Text = payload;
            if (!string.IsNullOrWhiteSpace(payload))
                Log("Countdown: " + payload);
        }

        private void HandleRoundStart(string payload)
        {
            // ROUND_START|roundId|requiredClicks|timeoutMs
            string[] parts = payload.Split('|');
            if (parts.Length < 3) return;

            _roundId = int.Parse(parts[0]);
            _requiredClicks = int.Parse(parts[1]);
            int timeoutMs = int.Parse(parts[2]);
            _clickCount = 0;

            EnsureSelectionDefaults();
            UpdateSelectionHighlights();

            _localClickDoneTcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            _remoteWinnerTcs = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            EnableRaceUi();
            Log("Round " + _roundId + " started. Required clicks: " + _requiredClicks);
            _ = RunLocalRaceWatcherAsync(_roundId, timeoutMs, _sessionCts?.Token ?? CancellationToken.None);
        }

        private void HandleRaceDone(string payload)
        {
            if (!_isHost) return;
            // RACE_DONE|roundId|attackerKind|targetKind
            string[] parts = payload.Split('|');
            if (parts.Length < 3) return;
            int roundId = int.Parse(parts[0]);
            if (roundId != _roundId) return;

            _remoteChosenAttackerKind = (HeroKind)int.Parse(parts[1]);
            _remoteChosenTargetKind = (HeroKind)int.Parse(parts[2]);
            _remoteChoiceReceived = true;

            _remoteRaceDoneTcs?.TrySetResult(true);
        }

        private void HandleRoundWinner(string payload)
        {
            // ROUND_WINNER|roundId|P1/P2
            string[] parts = payload.Split('|');
            if (parts.Length < 2) return;

            int roundId = int.Parse(parts[0]);
            string winnerSlot = parts[1];
            if (roundId != _roundId) return;

            Log("Round winner: " + winnerSlot);
            btnAttackRace.Enabled = false;
            _remoteWinnerTcs?.TrySetResult(winnerSlot);
        }

        private void HandleAttack(string payload)
        {
            // ATTACK|roundId|attackerSlot|attackerKind|defenderSlot|defenderKind|damage|defenderHpAfter|defenderDied
            string[] parts = payload.Split('|');
            if (parts.Length < 8) return;

            int roundId = int.Parse(parts[0]);
            string attackerSlot = parts[1];
            HeroKind attackerKind = (HeroKind)int.Parse(parts[2]);
            string defenderSlot = parts[3];
            HeroKind defenderKind = (HeroKind)int.Parse(parts[4]);
            int damage = int.Parse(parts[5]);
            int defenderHpAfter = int.Parse(parts[6]);
            bool defenderDied = parts[7] == "1";

            Log("Attack winner: " + attackerSlot);
            Log("Damage: " + attackerKind + " -> " + defenderSlot + " " + defenderKind + " for " + damage + " (HP now " + defenderHpAfter + ")");

            _ = PlayAttackAsync(roundId, attackerSlot, attackerKind, defenderSlot, defenderKind, defenderHpAfter, defenderDied);
        }

        private void HandleGameOver(string payload)
        {
            string winner = payload.Trim();
            Log("GAME OVER. Winner: " + winner);
            lblStatus.Text = "Winner: " + winner;
            btnAttackRace.Enabled = false;
            btnLoadTeam.Enabled = false;
            btnStartFight.Enabled = false;
        }

        // -------------------- Animation + UI Updates --------------------

        private async Task PlayAttackAsync(int roundId, string attackerSlot, HeroKind attackerKind, string defenderSlot, HeroKind defenderKind, int defenderHpAfter, bool defenderDied)
        {
            if (_sessionCts == null) return;

            await _animLock.WaitAsync();
            try
            {
                PictureBox attackerPic = GetHeroPictureBox(attackerSlot, attackerKind);
                PictureBox defenderPic = GetHeroPictureBox(defenderSlot, defenderKind);

                if (attackerPic == null || defenderPic == null)
                {
                    ApplyDefenderHp(defenderSlot, defenderKind, defenderHpAfter);
                    UpdateAllUi();
                    return;
                }

                Point start = _startPos[attackerPic];
                int dy = attackerSlot == "P1" ? 40 : -40;
                Point target = new Point(start.X, start.Y + dy);

                await MovePictureAsync(attackerPic, start, target, 10, _sessionCts.Token);

                await PlayEffectAsync(attackerKind, attackerPic, defenderPic, _sessionCts.Token);

                await Task.Delay(80, _sessionCts.Token);

                ApplyDefenderHp(defenderSlot, defenderKind, defenderHpAfter);
                UpdateAllUi();

                await Task.Delay(120, _sessionCts.Token);
                await MovePictureAsync(attackerPic, target, start, 10, _sessionCts.Token);

                if (defenderDied)
                    Log(defenderSlot + " " + defenderKind + " defeated!");
            }
            catch (OperationCanceledException)
            {
            }
            finally
            {
                _animLock.Release();
            }
        }

        private async Task PlayEffectAsync(HeroKind attackerKind, PictureBox attackerPic, PictureBox defenderPic, CancellationToken token)
        {
            if (picEffect == null)
                return;

            Image? effect = attackerKind switch
            {
                HeroKind.Archer => TryLoadAssetImage("effect_arrow.png") ?? MakeArrowEffect(),
                HeroKind.Mage => TryLoadAssetImage("effect_fire.png") ?? MakeFireEffect(),
                _ => TryLoadAssetImage("effect_slash.png") ?? MakeSlashEffect(),
            };

            if (effect == null)
                return;

            picEffect.Image = effect;
            picEffect.Visible = true;

            Point from = CenterOf(attackerPic, picEffect.Size);
            Point to = CenterOf(defenderPic, picEffect.Size);

            // Warrior effect is a quick flash on target.
            if (attackerKind == HeroKind.Warrior)
            {
                picEffect.Location = to;
                await Task.Delay(120, token);
                picEffect.Visible = false;
                return;
            }

            // Projectile for Archer/Mage
            picEffect.Location = from;
            await MovePictureAsync(picEffect, from, to, 12, token);
            await Task.Delay(80, token);
            picEffect.Visible = false;
        }

        private static Point CenterOf(PictureBox src, Size effectSize)
        {
            int x = src.Left + (src.Width / 2) - (effectSize.Width / 2);
            int y = src.Top + (src.Height / 2) - (effectSize.Height / 2);
            return new Point(x, y);
        }

        private async Task MovePictureAsync(PictureBox pic, Point from, Point to, int steps, CancellationToken token)
        {
            for (int i = 1; i <= steps; i++)
            {
                token.ThrowIfCancellationRequested();
                int x = from.X + (to.X - from.X) * i / steps;
                int y = from.Y + (to.Y - from.Y) * i / steps;
                pic.Location = new Point(x, y);
                await Task.Delay(15, token);
            }
        }

        private void ApplyDefenderHp(string defenderSlot, HeroKind kind, int hpAfter)
        {
            Player? p = defenderSlot == "P1" ? _p1 : _p2;
            if (p == null) return;
            for (int i = 0; i < p.Heroes.Count; i++)
            {
                if (p.Heroes[i] != null && p.Heroes[i].Kind == kind)
                {
                    p.Heroes[i].Health = hpAfter;
                    if (p.Heroes[i].Health < 0) p.Heroes[i].Health = 0;
                    return;
                }
            }
        }

        private void UpdateAllUi()
        {
            UpdateSide("P1", _p1);
            UpdateSide("P2", _p2);
        }

        private bool IsHeroAlive(string slot, HeroKind kind)
        {
            Player? p = slot == "P1" ? _p1 : _p2;
            if (p == null) return false;
            for (int i = 0; i < p.Heroes.Count; i++)
            {
                if (p.Heroes[i] != null && p.Heroes[i].Kind == kind)
                    return p.Heroes[i].IsAlive;
            }
            return false;
        }

        private HeroKind GetFirstAliveKind(string slot)
        {
            Player? p = slot == "P1" ? _p1 : _p2;
            if (p == null) return HeroKind.Warrior;
            Hero? h = p.GetFirstAliveHero();
            return h != null ? h.Kind : HeroKind.Warrior;
        }

        private void EnsureSelectionDefaults()
        {
            if (_localSlot != "P1" && _localSlot != "P2")
                return;

            string enemySlot = _localSlot == "P1" ? "P2" : "P1";

            if (!_attackerSelected || !IsHeroAlive(_localSlot, _selectedAttackerKind))
            {
                _selectedAttackerKind = GetFirstAliveKind(_localSlot);
                _attackerSelected = true;
            }

            if (!_targetSelected || !IsHeroAlive(enemySlot, _selectedTargetKind))
            {
                _selectedTargetKind = GetFirstAliveKind(enemySlot);
                _targetSelected = true;
            }
        }

        private void UpdateSelectionHighlights()
        {
            Color normal = Color.FromArgb(30, 30, 34);
            Color attackerSel = Color.FromArgb(40, 70, 120);
            Color targetSel = Color.FromArgb(120, 60, 60);

            // reset
            picTopWarrior.BackColor = normal;
            picTopMage.BackColor = normal;
            picTopArcher.BackColor = normal;
            picBottomWarrior.BackColor = normal;
            picBottomMage.BackColor = normal;
            picBottomArcher.BackColor = normal;

            if (_localSlot != "P1" && _localSlot != "P2")
                return;

            string enemySlot = _localSlot == "P1" ? "P2" : "P1";

            PictureBox attackerPic = GetHeroPictureBox(_localSlot, _selectedAttackerKind);
            PictureBox targetPic = GetHeroPictureBox(enemySlot, _selectedTargetKind);

            if (attackerPic != null) attackerPic.BackColor = attackerSel;
            if (targetPic != null) targetPic.BackColor = targetSel;
        }

        private void UpdateSide(string slot, Player? p)
        {
            if (p == null || p.Heroes.Count < 3) return;

            foreach (Hero h in p.Heroes)
            {
                if (h == null) continue;
                PictureBox pic = GetHeroPictureBox(slot, h.Kind);
                ProgressBar bar = GetHeroHpBar(slot, h.Kind);

                if (bar != null)
                {
                    if (bar.Maximum != h.MaxHealth) bar.Maximum = h.MaxHealth;
                    int v = h.Health;
                    if (v < 0) v = 0;
                    if (v > bar.Maximum) v = bar.Maximum;
                    bar.Value = v;
                }

                if (pic != null)
                    pic.Visible = h.IsAlive;
            }
        }

        private PictureBox GetHeroPictureBox(string slot, HeroKind kind)
        {
            if (slot == "P1")
            {
                return kind switch
                {
                    HeroKind.Warrior => picTopWarrior,
                    HeroKind.Mage => picTopMage,
                    _ => picTopArcher
                };
            }

            return kind switch
            {
                HeroKind.Warrior => picBottomWarrior,
                HeroKind.Mage => picBottomMage,
                _ => picBottomArcher
            };
        }

        private ProgressBar GetHeroHpBar(string slot, HeroKind kind)
        {
            if (slot == "P1")
            {
                return kind switch
                {
                    HeroKind.Warrior => hpTopWarrior,
                    HeroKind.Mage => hpTopMage,
                    _ => hpTopArcher
                };
            }

            return kind switch
            {
                HeroKind.Warrior => hpBottomWarrior,
                HeroKind.Mage => hpBottomMage,
                _ => hpBottomArcher
            };
        }

        private void SetRoleLabels()
        {
            if (_localSlot == "P1")
            {
                lblPlayerRole.Text = "You are Player 1 (TOP)" + (_isHost ? " - Host" : "");
                lblTopPlayer.Text = "Top: Player 1 (You)";
                lblBottomPlayer.Text = "Bottom: Player 2";
            }
            else if (_localSlot == "P2")
            {
                lblPlayerRole.Text = "You are Player 2 (BOTTOM)" + (_isHost ? " - Host" : "");
                lblTopPlayer.Text = "Top: Player 1";
                lblBottomPlayer.Text = "Bottom: Player 2 (You)";
            }
            else
            {
                lblPlayerRole.Text = "Role: (waiting...)";
                lblTopPlayer.Text = "Top: Player 1";
                lblBottomPlayer.Text = "Bottom: Player 2";
            }

            UpdateSelectionHighlights();
        }

        private void Log(string msg)
        {
            if (InvokeRequired)
            {
                BeginInvoke((Action)(() => Log(msg)));
                return;
            }

            lstLog.Items.Add(msg);
            if (lstLog.Items.Count > 0)
                lstLog.TopIndex = lstLog.Items.Count - 1;
        }

        private void ResetUiToIdle()
        {
            lblStatus.Text = "Status: Idle";
            lblCountdown.Text = "";
            lblPlayerRole.Text = "Role: --";
            lblTopPlayer.Text = "Top: Player 1";
            lblBottomPlayer.Text = "Bottom: Player 2";

            _attackerSelected = false;
            _targetSelected = false;
            _selectedAttackerKind = HeroKind.Warrior;
            _selectedTargetKind = HeroKind.Warrior;
            UpdateSelectionHighlights();

            btnHostGame.Enabled = true;
            btnJoinGame.Enabled = true;
            btnCancelSearch.Enabled = false;
            btnLoadTeam.Enabled = false;
            btnStartFight.Enabled = false;
            btnAttackRace.Enabled = false;

            hpTopWarrior.Value = 0;
            hpTopMage.Value = 0;
            hpTopArcher.Value = 0;
            hpBottomWarrior.Value = 0;
            hpBottomMage.Value = 0;
            hpBottomArcher.Value = 0;
        }

        private void ResetTeamState()
        {
            _localTeamReady = false;
            _remoteTeamReady = false;
            _localStartClicked = false;
            _remoteStartClicked = false;
            _fightCountdownStarted = false;
            _attackerSelected = false;
            _targetSelected = false;
            _remoteChoiceReceived = false;
        }

        private void ResetRoundState()
        {
            _roundId = 0;
            _requiredClicks = 0;
            _clickCount = 0;
            _localClickDoneTcs = null;
            _remoteWinnerTcs = null;
            _remoteRaceDoneTcs = null;
        }

        private void ApplyTeam(Player p)
        {
            if (p.Slot == "P1") _p1 = p;
            if (p.Slot == "P2") _p2 = p;
        }

        private string SerializeTeam(Player p)
        {
            return p.Heroes[0].ToWire() + ";" + p.Heroes[1].ToWire() + ";" + p.Heroes[2].ToWire();
        }

        private List<Hero> DeserializeTeam(string wire)
        {
            string[] parts = wire.Split(';');
            List<Hero> heroes = new List<Hero>();
            for (int i = 0; i < parts.Length; i++)
                heroes.Add(Hero.FromWire(parts[i]));
            return heroes;
        }

        private void EnsurePixelSprites()
        {
            // Prefer downloaded CC0 pixel art (Assets folder). Fallback to generated placeholders.
            Image? w = TryLoadAssetImage("hero_warrior.png");
            Image? m = TryLoadAssetImage("hero_mage.png");
            Image? a = TryLoadAssetImage("hero_archer.png");

            picTopWarrior.Image ??= w ?? MakePixelSprite(HeroKind.Warrior, top: true);
            picTopMage.Image ??= m ?? MakePixelSprite(HeroKind.Mage, top: true);
            picTopArcher.Image ??= a ?? MakePixelSprite(HeroKind.Archer, top: true);

            picBottomWarrior.Image ??= w ?? MakePixelSprite(HeroKind.Warrior, top: false);
            picBottomMage.Image ??= m ?? MakePixelSprite(HeroKind.Mage, top: false);
            picBottomArcher.Image ??= a ?? MakePixelSprite(HeroKind.Archer, top: false);
        }

        private Image? TryLoadAssetImage(string fileName)
        {
            try
            {
                string baseDir = AppDomain.CurrentDomain.BaseDirectory;
                string path = System.IO.Path.Combine(baseDir, "Assets", fileName);
                if (!System.IO.File.Exists(path))
                    return null;

                // Load without locking the file.
                byte[] bytes = System.IO.File.ReadAllBytes(path);
                using (var ms = new System.IO.MemoryStream(bytes))
                {
                    return Image.FromStream(ms);
                }
            }
            catch
            {
                return null;
            }
        }

        private static Bitmap MakeArrowEffect()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(90, 210, 135)))
                {
                    g.FillRectangle(b, 2, 8, 10, 2);
                    g.FillRectangle(b, 10, 6, 2, 6);
                    g.FillRectangle(b, 12, 7, 2, 4);
                }
                using (SolidBrush b = new SolidBrush(Color.FromArgb(18, 18, 20)))
                    g.FillRectangle(b, 3, 9, 1, 1);
            }
            return bmp;
        }

        private static Bitmap MakeFireEffect()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(235, 110, 70)))
                    g.FillEllipse(b, 3, 3, 10, 10);
                using (SolidBrush b = new SolidBrush(Color.FromArgb(255, 200, 120)))
                    g.FillEllipse(b, 6, 6, 5, 5);
            }
            return bmp;
        }

        private static Bitmap MakeSlashEffect()
        {
            Bitmap bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);
                using (Pen p = new Pen(Color.FromArgb(235, 230, 220), 2))
                    g.DrawLine(p, 3, 12, 12, 3);
                using (Pen p = new Pen(Color.FromArgb(18, 18, 20), 1))
                    g.DrawLine(p, 4, 12, 12, 4);
            }
            return bmp;
        }

        private Bitmap MakePixelSprite(HeroKind kind, bool top)
        {
            // 16x16 "pixel" sprite, scaled by PictureBox.
            Bitmap bmp = new Bitmap(16, 16);

            Color outline = Color.FromArgb(18, 18, 20);
            Color baseC = kind switch
            {
                HeroKind.Warrior => top ? Color.FromArgb(220, 90, 80) : Color.FromArgb(185, 70, 70),
                HeroKind.Mage => top ? Color.FromArgb(90, 150, 230) : Color.FromArgb(70, 120, 205),
                _ => top ? Color.FromArgb(90, 210, 135) : Color.FromArgb(70, 185, 110)
            };

            Color accent = kind switch
            {
                HeroKind.Warrior => Color.FromArgb(235, 210, 160),
                HeroKind.Mage => Color.FromArgb(240, 220, 255),
                _ => Color.FromArgb(210, 190, 120)
            };

            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.FromArgb(28, 28, 32));

                // border
                using (Pen p = new Pen(outline))
                    g.DrawRectangle(p, 0, 0, 15, 15);

                // body block
                using (SolidBrush b = new SolidBrush(baseC))
                    g.FillRectangle(b, 4, 5, 8, 8);

                // head
                using (SolidBrush b = new SolidBrush(accent))
                    g.FillRectangle(b, 6, 3, 4, 3);

                // class marker
                using (SolidBrush b = new SolidBrush(outline))
                {
                    if (kind == HeroKind.Warrior)
                    {
                        // sword
                        g.FillRectangle(b, 11, 6, 1, 5);
                        g.FillRectangle(b, 10, 8, 3, 1);
                    }
                    else if (kind == HeroKind.Mage)
                    {
                        // staff
                        g.FillRectangle(b, 4, 6, 1, 6);
                        g.FillRectangle(b, 3, 6, 3, 1);
                    }
                    else
                    {
                        // bow
                        g.FillRectangle(b, 11, 6, 1, 6);
                        g.FillRectangle(b, 9, 7, 2, 1);
                        g.FillRectangle(b, 9, 10, 2, 1);
                    }
                }
            }

            return bmp;
        }

        private void CacheStartPositions()
        {
            _startPos[picTopWarrior] = picTopWarrior.Location;
            _startPos[picTopMage] = picTopMage.Location;
            _startPos[picTopArcher] = picTopArcher.Location;
            _startPos[picBottomWarrior] = picBottomWarrior.Location;
            _startPos[picBottomMage] = picBottomMage.Location;
            _startPos[picBottomArcher] = picBottomArcher.Location;
        }

        // -------------------- Cleanup --------------------

        private async Task CleanupAsync(string reason)
        {
            try { _readyWindowCts?.Cancel(); } catch { }
            try { _matchCts?.Cancel(); } catch { }
            try { _sessionCts?.Cancel(); } catch { }

            if (_conn != null)
            {
                try { await _conn.DisposeAsync(); } catch { }
                _conn = null;
            }

            _readyWindowCts = null;
            _matchCts = null;
            _sessionCts = null;

            _isHost = false;
            _localSlot = "";
            _p1 = null;
            _p2 = null;
            ResetTeamState();
            ResetRoundState();

            if (!IsHandleCreated) return;
            BeginInvoke((Action)(() =>
            {
                ResetUiToIdle();
                Log("Reset: " + reason);
            }));
        }
    }
}
