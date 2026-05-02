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

        //
        // Constructs the main game UI and initializes designer-created controls.
        //
        public Form1()
        {
            InitializeComponent();
        }

        //
        // Form load: sets up visuals and resets the UI.
        //
        // @param sender: Event source.
        // @param e: Event args.
        // @notes: Loads sprites (or generates placeholders), caches start positions for animations.
        //
        private void Form1_Load(object sender, EventArgs e)
        {
            EnsurePixelSprites();
            CacheStartPositions();
            ResetUiToIdle();
        }

        //
        // Runs cleanup when the form is closing.
        //
        // @param e: Closing args.
        // @notes: Cancels background work and disposes the pipe connection.
        //
        protected override async void OnFormClosing(FormClosingEventArgs e)
        {
            base.OnFormClosing(e);
            await CleanupAsync("Form closing");
        }

        // -------------------- UI Events (Designer Wired) --------------------

        //
        // UI: Host Game button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private async void btnHostGame_Click(object sender, EventArgs e)
        {
            await HostGameAsync();
        }

        //
        // UI: Join Game button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private async void btnJoinGame_Click(object sender, EventArgs e)
        {
            await JoinGameAsync();
        }

        //
        // UI: Cancel search button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private void btnCancelSearch_Click(object sender, EventArgs e)
        {
            CancelSearch();
        }

        //
        // UI: Load Team button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private async void btnLoadTeam_Click(object sender, EventArgs e)
        {
            await LoadTeamAsync();
        }

        //
        // UI: Start Fight button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private async void btnStartFight_Click(object sender, EventArgs e)
        {
            await StartFightClickedAsync();
        }

        //
        // UI: Attack Race button.
        //
        // @param sender: Button.
        // @param e: Event args.
        //
        private void btnAttackRace_Click(object sender, EventArgs e)
        {
            AttackRaceClick();
        }

        //
        // UI: Click top Warrior (P1).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picTopWarrior_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Warrior);

        //
        // UI: Click top Mage (P1).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picTopMage_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Mage);

        //
        // UI: Click top Archer (P1).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picTopArcher_Click(object sender, EventArgs e) => OnHeroPictureClicked("P1", HeroKind.Archer);

        //
        // UI: Click bottom Warrior (P2).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picBottomWarrior_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Warrior);

        //
        // UI: Click bottom Mage (P2).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picBottomMage_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Mage);

        //
        // UI: Click bottom Archer (P2).
        //
        // @param sender: PictureBox.
        // @param e: Event args.
        //
        private void picBottomArcher_Click(object sender, EventArgs e) => OnHeroPictureClicked("P2", HeroKind.Archer);

        //
        // Applies a hero click to selection state.
        //
        // @param slot: Which side was clicked ("P1" or "P2").
        // @param kind: Which hero was clicked.
        //
        // @notes
        // - Clicking your own side sets the attacker.
        // - Clicking the other side sets the target.
        // - Dead heroes are ignored.
        //
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

        // Hosts a game session (P1) and waits for a client to join.
        //
        // @notes: Logic: Creates/starts a named-pipe server via MatchmakingManager, hooks message events, then starts a short cancel window where either side can cancel before proceeding.
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

        // Joins a hosted game session (becomes P2 after ROLE assignment).
        //
        // @notes: Logic: Connects as a named-pipe client, waits for host role assignment, then participates in the cancel window handshake.
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

        // Cancels matchmaking/search and starts cleanup.
        //
        // @notes: Logic: Cancels the matchmaking/cancel-window tokens, optionally notifies the remote side, then triggers async cleanup to reset UI/state.
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

        // Hooks up connection events so the form can react to messages/disconnects.
        //
        // @param conn: Active pipe connection.
        private void HookConnection(GameConnection conn)
        {
            conn.MessageReceived += OnNetMessage;
            conn.Disconnected += (reason) =>
            {
                Log("Disconnected: " + reason);
                _ = CleanupAsync("Disconnected");
            };
        }

        // Starts the shared "cancel window" countdown before unlocking team loading.
        //
        // @param seconds: Number of seconds to show the countdown.
        // @notes: Logic: Host sends READYWIN, both sides tick a countdown label; when it finishes, host sends UNLOCK_LOAD and both sides enable loading.
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

        // Enables the UI flow for Activity 2 (team loading) after matchmaking is confirmed.
        private void UnlockTeamLoading()
        {
            Log("Match confirmed. Load Team unlocked.");
            lblStatus.Text = "Connected. Load your team.";
            btnLoadTeam.Enabled = true;
        }

        // -------------------- Activity 2: Team + Start Fight --------------------

        // Loads the local team and sends the TEAM_READY message to the other player.
        //
        // @notes: Logic: Uses ArenaLoader to build heroes concurrently, stores them into P1/P2 state, and serializes them so the opponent can reconstruct the same roster.
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

        // Updates UI/state when both local and remote teams are marked ready.
        private void OnBothTeamsReady()
        {
            Log("Both teams ready." );
            lblStatus.Text = "Both teams ready. Click Start Fight.";
            btnStartFight.Enabled = true;
        }

        // Handles "Start Fight" click; sends readiness and (host) triggers the countdown when both ready.
        //
        // @notes: Logic: Both players send START_READY. Host starts the countdown only after receiving both.
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

        // Host-only: broadcasts the fight countdown and starts the first round.
        //
        // @notes: Logic: Broadcasts FIGHT_CD ticks so both clients stay in sync, then starts Activity 3 rounds.
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

        // UI handler for the "Attack Race" button; increments click counter and completes the local race.
        //
        // @notes: Logic: When required clicks are reached it completes a TCS. The joiner also sends RACE_DONE (including current attacker/target selection) to the host.
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

        // Host-only: runs a single round (race winner selection, attack resolution, and broadcasts).
        //
        // @notes: Logic: Starts the round, waits for local vs remote completion (or timeout), determines winner, resolves the attack using CombatEngine, broadcasts results, and loops to next round.
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

        // Enables the race UI for the current round and updates the status prompt.
        private void EnableRaceUi()
        {
            btnAttackRace.Enabled = true;
            lblStatus.Text = "Speed race: click " + _requiredClicks + " times!";
        }

        // Local helper that times out the race if clicks aren't finished in time.
        //
        // @param roundId: Round identifier for which this watcher is valid.
        // @param timeoutMs: How long to wait before forcing completion.
        // @param token: Cancellation token for aborting the watcher.
        // @notes: Logic: After the timeout, completes the local TCS (if still pending) so the round can continue.
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

        // Sends a protocol message to the remote side and applies it locally on the host.
        //
        // @param msg: Single-line protocol message (e.g., "TYPE|payload").
        // @notes: Logic: Host sends the line over the pipe, then calls OnNetMessage locally so it runs the same handler code path.
        private async Task BroadcastAsync(string msg)
        {
            if (_conn == null || !_conn.IsConnected || _sessionCts == null)
                return;

            await _conn.SendAsync(msg, _sessionCts.Token);
            OnNetMessage(msg); // host also applies locally
        }

        // Parses a raw network line and dispatches to the corresponding handler.
        //
        // @param raw: Raw line received from the named pipe.
        // @notes: Logic: Marshals to the UI thread (InvokeRequired) then routes by the opcode prefix before the first '|'.
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

        // Handles TEAM_READY messages by reconstructing the sender's hero roster.
        //
        // @param payload: "slot|hero;hero;hero" payload.
        // @notes: Logic: Deserializes heroes, stores them into the correct player slot, updates UI, and advances to "both teams ready" if applicable.
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

        // Handles START_READY by marking the remote side as having clicked start.
        //
        // @param payload: Sender slot ("P1" or "P2").
        // @notes: Logic: Once both sides have clicked, host triggers the shared countdown.
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

        // Updates the countdown label based on FIGHT_CD payload.
        //
        // @param payload: Countdown text ("3","2","1","FIGHT", or empty to clear).
        private void HandleFightCountdown(string payload)
        {
            lblCountdown.Text = payload;
            if (!string.IsNullOrWhiteSpace(payload))
                Log("Countdown: " + payload);
        }

        // Initializes per-round race state based on ROUND_START broadcast.
        //
        // @param payload: "roundId|requiredClicks|timeoutMs" payload.
        // @notes: Logic: Resets counters, ensures valid selections, enables race UI, and starts the local watcher.
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

        // Host-only: receives remote race completion and the remote's chosen attacker/target.
        //
        // @param payload: "roundId|attackerKind|targetKind" payload.
        // @notes: Logic: Stores the remote selection and completes the host-side TCS used to decide the race winner.
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

        // Handles ROUND_WINNER broadcast by disabling race UI and completing the winner TCS.
        //
        // @param payload: "roundId|P1" or "roundId|P2" payload.
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

        // Handles ATTACK broadcasts by animating the attack and applying defender HP.
        //
        // @param payload: Attack payload including slots, kinds, damage and resulting HP.
        // @notes: Logic: Parses fields, logs them, then runs PlayAttackAsync to animate and apply state.
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

        // Handles GAME_OVER by showing the winner and disabling gameplay buttons.
        //
        // @param payload: Winner slot string ("P1" or "P2").
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

        // Animates an attack and applies the defender's HP change.
        //
        // @param roundId: Round identifier (informational; not used for gating here).
        // @param attackerSlot: Attacker slot ("P1" or "P2").
        // @param attackerKind: Attacker hero kind.
        // @param defenderSlot: Defender slot ("P1" or "P2").
        // @param defenderKind: Defender hero kind.
        // @param defenderHpAfter: Defender HP after applying damage.
        // @param defenderDied: True if the defender died from this attack.
        // @notes: Logic: Uses a semaphore to serialize animations, moves the attacker forward/back, plays an effect, then updates in-memory HP + progress bars.
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

        // Plays a per-class visual effect for an attack.
        //
        // @param attackerKind: Hero class used to pick the effect (arrow/fire/slash).
        // @param attackerPic: Attacker picture box (effect origin).
        // @param defenderPic: Defender picture box (effect destination/target).
        // @param token: Cancellation token used during animations.
        // @notes: Logic: Warrior uses a brief flash at the target; Archer/Mage animate a projectile from attacker to defender.
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

        // Computes the top-left point that centers an effect image over a source PictureBox.
        //
        // @param src: Source control to center over.
        // @param effectSize: Effect size to center.
        // @returns: A point suitable for setting Control.Location.
        private static Point CenterOf(PictureBox src, Size effectSize)
        {
            int x = src.Left + (src.Width / 2) - (effectSize.Width / 2);
            int y = src.Top + (src.Height / 2) - (effectSize.Height / 2);
            return new Point(x, y);
        }

        // Moves a PictureBox smoothly from one point to another in fixed steps.
        //
        // @param pic: Control to move.
        // @param from: Start location.
        // @param to: End location.
        // @param steps: Number of interpolation steps.
        // @param token: Cancellation token for aborting movement.
        // @notes: Logic: Linear interpolation per step with a small delay to create motion.
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

        // Applies the post-attack HP value to the defender hero in local in-memory state.
        //
        // @param defenderSlot: Defender slot ("P1" or "P2").
        // @param kind: Defender hero kind to update.
        // @param hpAfter: HP value to set (clamped at 0).
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

        // Refreshes both sides' UI from the current player state.
        private void UpdateAllUi()
        {
            UpdateSide("P1", _p1);
            UpdateSide("P2", _p2);
        }

        // Checks whether the specified hero is alive in the specified slot.
        //
        // @param slot: "P1" or "P2".
        // @param kind: Hero kind to check.
        // @returns: True if the hero exists and has health > 0.
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

        // Gets the kind of the first living hero for a given slot.
        //
        // @param slot: "P1" or "P2".
        // @returns: The first alive hero kind, or Warrior as a safe default.
        private HeroKind GetFirstAliveKind(string slot)
        {
            Player? p = slot == "P1" ? _p1 : _p2;
            if (p == null) return HeroKind.Warrior;
            Hero? h = p.GetFirstAliveHero();
            return h != null ? h.Kind : HeroKind.Warrior;
        }

        // Ensures attacker/target selections are set to living heroes (auto-fills if unset or dead).
        //
        // @notes: Logic: If the current selection is invalid, it picks the first living hero on that side.
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

        // Updates PictureBox background colors to reflect current attacker and target selections.
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

        // Updates one side's hero visibility and HP bars from the given player state.
        //
        // @param slot: "P1" or "P2".
        // @param p: Player state for that slot.
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

        // Maps a slot + hero kind to the corresponding hero PictureBox control.
        //
        // @param slot: "P1" (top) or "P2" (bottom).
        // @param kind: Hero kind to map.
        // @returns: The matching PictureBox control.
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

        // Maps a slot + hero kind to the corresponding HP ProgressBar control.
        //
        // @param slot: "P1" (top) or "P2" (bottom).
        // @param kind: Hero kind to map.
        // @returns: The matching ProgressBar control.
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

        // Updates role labels to reflect the assigned local slot and host/join status.
        //
        // @notes: Logic: Also refreshes selection highlights so the UI matches current local/remote orientation.
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

        // Appends a message to the on-screen log list.
        //
        // @param msg: Message text to append.
        // @notes: Logic: Marshals to the UI thread when called from background tasks.
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

        // Resets UI controls to the initial idle state.
        //
        // @notes: Logic: Clears labels, disables gameplay buttons, resets selection, and clears HP bars.
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

        // Clears team/start-related state flags for a new session.
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

        // Clears round/race-related state so a new match/round sequence can start cleanly.
        private void ResetRoundState()
        {
            _roundId = 0;
            _requiredClicks = 0;
            _clickCount = 0;
            _localClickDoneTcs = null;
            _remoteWinnerTcs = null;
            _remoteRaceDoneTcs = null;
        }

        // Writes a loaded/deserialized team into the correct local player slot (P1 or P2).
        //
        // @param p: Player object containing slot + hero list.
        private void ApplyTeam(Player p)
        {
            if (p.Slot == "P1") _p1 = p;
            if (p.Slot == "P2") _p2 = p;
        }

        // Serializes a player's hero roster for sending over the network.
        //
        // @param p: Player whose Player.Heroes are serialized.
        // @returns: Three hero wire strings joined by ';'.
        private string SerializeTeam(Player p)
        {
            return p.Heroes[0].ToWire() + ";" + p.Heroes[1].ToWire() + ";" + p.Heroes[2].ToWire();
        }

        // Deserializes a roster wire string into a list of heroes.
        //
        // @param wire: Hero list wire string produced by SerializeTeam.
        // @returns: List of heroes in the same order they were serialized.
        private List<Hero> DeserializeTeam(string wire)
        {
            string[] parts = wire.Split(';');
            List<Hero> heroes = new List<Hero>();
            for (int i = 0; i < parts.Length; i++)
                heroes.Add(Hero.FromWire(parts[i]));
            return heroes;
        }

        // Ensures each hero PictureBox has an image (loaded from Assets or generated placeholder).
        //
        // @notes: Logic: Attempts to load CC0 pixel-art PNGs from the output `Assets` folder; if missing, generates small 16x16 pixel-style bitmaps.
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

        // Attempts to load an image from the runtime output Assets folder.
        //
        // @param fileName: File name within the `Assets` output folder.
        // @returns: The loaded image, or null if not found/unreadable.
        // @notes: Logic: Reads the entire file into memory first to avoid locking the file on disk.
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

        // Generates a tiny pixel-style arrow effect bitmap (fallback when asset PNG missing).
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

        // Generates a tiny pixel-style fire effect bitmap (fallback when asset PNG missing).
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

        // Generates a tiny pixel-style slash effect bitmap (fallback when asset PNG missing).
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

        // Generates a 16x16 pixel-style hero sprite bitmap as a placeholder.
        //
        // @param kind: Hero kind used to pick colors and a small class marker.
        // @param top: True for P1 (slightly brighter palette); false for P2.
        // @returns: A small bitmap intended to be scaled by the PictureBox.
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

        // Caches initial hero PictureBox positions for later animation resets.
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

        // Cancels background work, disposes the connection, and resets state/UI.
        //
        // @param reason: Short reason used for logging/debugging.
        // @notes: Logic: Cancels CTSs, disposes the pipe, clears state, then marshals a UI reset back to the UI thread.
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
