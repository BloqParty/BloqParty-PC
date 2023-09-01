﻿using BeatSaberMarkupLanguage;
using BeatSaberMarkupLanguage.Attributes;
using BeatSaberMarkupLanguage.Components;
using BeatSaberMarkupLanguage.Parser;
using BeatSaberMarkupLanguage.ViewControllers;
using BedroomPartyLeaderboard.Utils;
using HMUI;
using IPA.Utilities;
using IPA.Utilities.Async;
using LeaderboardCore.Interfaces;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TMPro;
using UnityEngine;
using Zenject;
using static BedroomPartyLeaderboard.Utils.UIUtils;
using static LeaderboardTableView;
using Button = UnityEngine.UI.Button;

namespace BedroomPartyLeaderboard.UI.Leaderboard
{
    [HotReload(RelativePathToLayout = @"./BSML/LeaderboardView.bsml")]
    [ViewDefinition("BedroomPartyLeaderboard.UI.Leaderboard.BSML.LeaderboardView.bsml")]
    internal class LeaderboardView : BSMLAutomaticViewController, INotifyLeaderboardSet, IInitializable
    {
        [Inject] private PlatformLeaderboardViewController _plvc;
        [Inject] PlayerUtils _playerUtils;
        [Inject] PanelView _panelView;
        [Inject] RequestUtils _requestUtils;
        [Inject] LeaderboardData _leaderboardData;
        [Inject] private ResultsViewController _resultsViewController;
        [Inject] UIUtils _uiUtils;

        public IDifficultyBeatmap currentDifficultyBeatmap;
        public IDifficultyBeatmapSet currentDifficultyBeatmapSet;

        private string currentSongLinkLBWebView = string.Empty;
        public static LeaderboardData.LeaderboardEntry[] buttonEntryArray = new LeaderboardData.LeaderboardEntry[10];
        public string sortMethod = "top";

        [UIComponent("leaderboardTableView")]
        private LeaderboardTableView leaderboardTableView = null;

        [UIComponent("leaderboardTableView")]
        private Transform leaderboardTransform = null;

        [UIComponent("myHeader")]
        private Backgroundable myHeader;

        [UIComponent("headerText")]
        private TextMeshProUGUI headerText;

        [UIComponent("errorText")]
        private TextMeshProUGUI errorText;

        [UIObject("loginKeyboard")]
        public GameObject loginKeyboard;

        [UIObject("loginButton")]
        public GameObject loginButton;

        [UIValue("imageHolders")]
        [Inject] public List<ImageHolder> _ImageHolders;

        [UIValue("buttonHolders")]
        [Inject] private List<ButtonHolder> Buttonholders;

        [UIComponent("scoreInfoModal")]
        [Inject] private ScoreInfoModal scoreInfoModal;

        [UIComponent("up_button")]
        private Button up_button;

        [UIComponent("down_button")]
        private Button down_button;

        [UIObject("loadingLB")]
        private GameObject loadingLB;

        public int page = 0;
        public int totalPages;

        [UIAction("OnPageUp")]
        private void OnPageUp() => UpdatePageChanged(-1);

        [UIAction("OnPageDown")]
        private void OnPageDown() => UpdatePageChanged(1);

        private void UpdatePageChanged(int inc)
        {
            page = Mathf.Clamp(page + inc, 0, totalPages - 1);
            UpdatePageButtons();
            OnLeaderboardSet(currentDifficultyBeatmap);
        }

        public void UpdatePageButtons()
        {
            if (sortMethod == "around")
            {
                up_button.interactable = false;
                down_button.interactable = false;
                return;
            }
            up_button.interactable = (page > 0);
            down_button.interactable = (page < totalPages - 1);
        }

        [UIParams]
        BSMLParserParams parserParams;

        private GameObject _loadingControl;
        private ImageView _imgView;

        internal static readonly FieldAccessor<ImageView, float>.Accessor ImageSkew = FieldAccessor<ImageView, float>.GetAccessor("_skew");
        internal static readonly FieldAccessor<ImageView, bool>.Accessor ImageGradient = FieldAccessor<ImageView, bool>.GetAccessor("_gradient");

        [UIAction("#post-parse")]
        private void PostParse()
        {
            myHeader.background.material = Utilities.ImageResources.NoGlowMat;
            _loadingControl = leaderboardTransform.Find("LoadingControl").gameObject;
            var loadingContainer = _loadingControl.transform.Find("LoadingContainer");
            loadingContainer.gameObject.SetActive(false);
            Destroy(loadingContainer.Find("Text").gameObject);
            Destroy(_loadingControl.transform.Find("RefreshContainer").gameObject);
            Destroy(_loadingControl.transform.Find("DownloadingContainer").gameObject);
            _imgView = myHeader.background as ImageView;
            _imgView.color = Constants.BP_COLOR;
            _imgView.color0 = Constants.BP_COLOR;
            _imgView.color1 = Constants.BP_COLOR;
            ImageSkew(ref _imgView) = 0.18f;
            ImageGradient(ref _imgView) = true;
        }
        private void FuckOffButtons() => Buttonholders.ForEach(Buttonholders => Buttonholders.infoButton.gameObject.SetActive(false));

        [UIAction("openLBWebView")]
        public void openLBWebView()
        {
            if (!(String.IsNullOrEmpty(currentSongLinkLBWebView) || currentSongLinkLBWebView.Contains(" "))) Application.OpenURL(currentSongLinkLBWebView);
        }

        [UIAction("openBUGWebView")]
        public void openBUGWebView() => Application.OpenURL(Constants.BUG_REPORT_LINK);

        [UIAction("OnIconSelected")]
        private void OnIconSelected(SegmentedControl segmentedControl, int index)
        {
            if (index == 0) sortMethod = "top";
            else if (index == 1) sortMethod = "around";
            else sortMethod = "top";
            page = 0;
            UpdatePageButtons();
            OnLeaderboardSet(currentDifficultyBeatmap);
        }

        [UIValue("leaderboardIcons")]
        private List<IconSegmentedControl.DataItem> leaderboardIcons
        {
            get
            {
                return new List<IconSegmentedControl.DataItem>()
                {
                    new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("BedroomPartyLeaderboard.Images.Globe.png"), "Bedroom Party"),
                    new IconSegmentedControl.DataItem(Utilities.FindSpriteInAssembly("BedroomPartyLeaderboard.Images.Player.png"), "Around you")
                };
            }
        }

        public void showInfoModal()
        {
            parserParams.EmitEvent("showInfoModal");
        }


        [UIComponent("playlistButton")]
        public Button playlistButton;

        [UIAction("downloadRankedPlaylist")]
        public void downloadRankedPlaylist() => UnityMainThreadTaskScheduler.Factory.StartNew(() => _requestUtils.FUCKOFFPLAYLIST());

        [UIAction("openWebsite")]
        public void openWebsite() => Application.OpenURL("https://thebedroom.party");

        protected override void DidActivate(bool firstActivation, bool addedToHierarchy, bool screenSystemEnabling)
        {
            base.DidActivate(firstActivation, addedToHierarchy, screenSystemEnabling);
            if (!base.isActiveAndEnabled) return;
            if (!_plvc) return;
            var header = _plvc.transform.Find("HeaderPanel");
            if (firstActivation)
            {
                _panelView.prompt_loader.SetActive(true);
                _panelView.promptText.gameObject.SetActive(true);
                _panelView.promptText.text = "Authenticating...";

                _playerUtils.LoginUser();
            }
            _plvc.GetComponentInChildren<TextMeshProUGUI>().color = new Color(0, 0, 0, 0);
        }

        protected override void DidDeactivate(bool removedFromHierarchy, bool screenSystemDisabling)
        {
            base.DidDeactivate(removedFromHierarchy, screenSystemDisabling);
            if (!_plvc) return;
            if (!_plvc.isActivated) return;
            page = 0;
            parserParams.EmitEvent("hideInfoModal");
            _plvc.GetComponentInChildren<TextMeshProUGUI>().color = Color.white;
        }

        public void OnLeaderboardSet(IDifficultyBeatmap difficultyBeatmap)
        {
            currentDifficultyBeatmap = difficultyBeatmap;
            UnityMainThreadTaskScheduler.Factory.StartNew(() => realLeaderboardSet(difficultyBeatmap));
        }

        private async Task realLeaderboardSet(IDifficultyBeatmap difficultyBeatmap)
        {
            string errorReason = "Error";
            if (!_plvc || !_plvc.isActiveAndEnabled) return;

            await Task.Delay(1);
            FuckOffButtons();
            ByeImages();
            leaderboardTableView.SetScores(null, -1);

            if (_playerUtils.isAuthed)
            {
                errorText.gameObject.SetActive(false);
                errorReason = "Auth Fail";
                return;
            }

            loadingLB.gameObject.SetActive(true);
            errorText.gameObject.SetActive(false);

            await Task.Delay(1);

            if (!_plvc || !_plvc.isActiveAndEnabled) return;

            string mapId = difficultyBeatmap.level.levelID.Substring(13);
            int difficulty = difficultyBeatmap.difficultyRank;
            string mapType = difficultyBeatmap.parentDifficultyBeatmapSet.beatmapCharacteristic.serializedName;
            string balls = mapId + "_" + mapType + difficulty.ToString(); // BeatMap Allocated Level Label String
            currentSongLinkLBWebView = $"https://thebedroom.party/?board={balls}";
            _requestUtils.GetBeatMapData(balls, page, result =>
            {
                UnityMainThreadTaskScheduler.Factory.StartNew(() =>
                {
                    if (!_plvc || !_plvc.isActiveAndEnabled) return;
                    HelloIMGLoader();

                    if (result.Item3 != 0)
                    {
                        _panelView.playerGlobalRank.text = $"#{result.Item3}";
                    }

                    _panelView.playerPP.text = $"{result.Item6.ToString("F2")}pp";

                    if (result.Item5 != 0)
                    {
                        headerText.SetText($"RANKED - {result.Item5.ToString("F2")}<b>✰</b>");
                    }
                    else
                    {
                        headerText.SetText("UNRANKED");
                    }

                    if (result.Item2 != null)
                    {
                        if (result.Item2.Count == 0)
                        {
                            errorReason = "No Scores Yet!";
                            leaderboardTableView.SetScores(null, -1);
                            errorText.gameObject.SetActive(true);
                            loadingLB.gameObject.SetActive(false);
                            ByeIMGLoader();
                            UpdatePageButtons();
                        }
                        else
                        {
                            leaderboardTableView.SetScores(CreateLeaderboardData(result.Item2, page), -1);
                            _uiUtils.RichMyText(leaderboardTableView);
                            loadingLB.gameObject.SetActive(false);
                        }
                    }
                    else
                    {
                        errorReason = "No Scores Yet!";
                        leaderboardTableView.SetScores(null, -1);
                        errorText.gameObject.SetActive(true);
                        loadingLB.gameObject.SetActive(false);
                        ByeIMGLoader();
                        errorText.gameObject.SetActive(true);
                    }
                    errorText.text = errorReason;
                    _uiUtils.SetProfiles(result.Item2);
                    totalPages = result.Item4;
                    UpdatePageButtons();
                });
            });
        }

        private void ByeImages() => _ImageHolders.ForEach(holder => holder.profileImage.gameObject.SetActive(false));
        private void HelloIMGLoader() => _ImageHolders.ForEach(holder => holder.profileloading.SetActive(true));
        private void ByeIMGLoader() => _ImageHolders.ForEach(holder => holder.profileloading.SetActive(false));

        public List<ScoreData> CreateLeaderboardData(List<LeaderboardData.LeaderboardEntry> leaderboard, int page)
        {
            List<ScoreData> tableData = new List<ScoreData>();
            for (int i = 0; i < leaderboard.Count; i++)
            {
                int score = leaderboard[i].score;
                tableData.Add(CreateLeaderboardEntryData(leaderboard[i], score));
                _ImageHolders[i].setProfileImage(leaderboard[i].userID);
                buttonEntryArray[i] = leaderboard[i];
                Buttonholders[i].infoButton.gameObject.SetActive(true);
            }
            return tableData;
        }

        public ScoreData CreateLeaderboardEntryData(LeaderboardData.LeaderboardEntry entry, int score)
        {
            string formattedAcc = string.Format(" - (<color=#ffd42a>{0:0.00}%</color>)", entry.acc);
            string formattedCombo = "";
            if (entry.fullCombo) formattedCombo = " -<color=green> FC </color>";
            else formattedCombo = string.Format(" - <color=red>x{0} </color>", entry.badCutCount + entry.missCount);

            string formattedPP = string.Empty;
            if (entry.PP > 0) formattedPP = $" - <color=#AEC6CF>{entry.PP.ToString("F2")}</color>pp";
            string formattedMods = string.Format("  <size=60%>{0}</size>", entry.mods);

            string result;
            if (entry.userID == "3033139560125578") entry.userName = $"<color=blue>{entry.userName}</color>";
            result = "<size=100%>" + entry.userName.TrimEnd() + formattedAcc + formattedCombo + formattedMods + "</size>";
            return new ScoreData(score, result, entry.rank, false);
        }

        public void Initialize() => _resultsViewController.continueButtonPressedEvent += FUCKOFFIHATETHISIWANTTODIE;
        public void FUCKOFFIHATETHISIWANTTODIE(ResultsViewController resultsViewController) => OnLeaderboardSet(currentDifficultyBeatmap);
    }
}