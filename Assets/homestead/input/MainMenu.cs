﻿using UnityEngine;
using System.Collections;
using System;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using System.Collections.Generic;
using RedHomestead.Persistence;
using UnityStandardAssets.CrossPlatformInput;
using RedHomestead.Geography;
using RedHomestead.Perks;
using RedHomestead.Economy;
using RedHomestead.GameplayOptions;

[Serializable]
public abstract class MainMenuView
{
    public Transform CameraAnchor;
    public RectTransform CanvasParent;
    internal int LogoBottom, LogoRight;

    public void AfterTransitionTo()
    {
        CanvasParent.gameObject.SetActive(true);
        this._AfterTransitionTo();
    }
    public void BeforeTransitionAway()
    {
        CanvasParent.gameObject.SetActive(false);
        this._BeforeTransitionAway();
    }

    protected abstract void _AfterTransitionTo();
    protected abstract void _BeforeTransitionAway();
}

[Serializable]
public class ScoutView: MainMenuView
{
    public RectTransform ClaimHomesteadButton, SelectLocationButton;
    public Transform ScoutRegions, ScoutOrreyVertical, ScoutOrreyHorizontal, ScoutCursor;
    public Light Sun;
    public Behaviour Halo;
    public Spin PlanetSpin;
    public Text RegionName, RegionSolar, RegionMinerals, RegionWater, RegionRemote, RegionMultiplier, LatLongText;

    public void FillScoutInfo(MarsRegion region, LatLong latlong)
    {
        RegionName.text = region.Name();
        LatLongText.text = latlong.ToString();
        RegionWater.text = region.Data().WaterMultiplierString;
        RegionMinerals.text = region.Data().MineralMultiplierString;
        RegionSolar.text = region.Data().SolarMultiplierString;
    }

    internal void ToggleScoutMode(bool isScout)
    {
        RenderSettings.ambientLight = isScout ? new Color(1, 1, 1, .5f) : new Color(0, 0, 0, 0);

        Halo.enabled = !isScout;
        Sun.enabled = !isScout;
        ScoutRegions.gameObject.SetActive(isScout);
        CanvasParent.gameObject.SetActive(isScout);
        PlanetSpin.enabled = !isScout;

        if (isScout)
            PlanetSpin.transform.localRotation = Quaternion.Euler(-90, -90, 0);
    }

    protected override void _AfterTransitionTo()
    {
        ToggleScoutMode(true);
    }

    protected override void _BeforeTransitionAway()
    {
        ToggleScoutMode(false);
    }
}

[Serializable]
public class FinanceAndSupplyView: MainMenuView
{
    public Text StartingFunds, AllocatedSupplyFunds, RemainingFunds;

    public void RefreshFunds(NewGameChoices choices)
    {
        StartingFunds.text = String.Format("Starting Funds: ${0:#,##0}k", choices.StartingFunds / 1000);
        RemainingFunds.text = String.Format("Remaining Funds: ${0:#,##0}k", choices.RemainingFunds / 1000);
    }

    protected override void _AfterTransitionTo()
    {
    }

    protected override void _BeforeTransitionAway()
    {
    }
}

[Serializable]
public class TitleView: MainMenuView
{
    protected override void _AfterTransitionTo()
    {
    }

    protected override void _BeforeTransitionAway()
    {
    }
}

public enum MainMenuCameraView { Title, Scout, Supply }

public class MainMenu : MonoBehaviour {
    public Image BigLogo;
    public RectTransform QuickstartBackdrop, QuickstartTrainingEquipmentRow;
    public Button LoadButton;
    public ScoutView ScoutView;
    public FinanceAndSupplyView FinanceAndSupplyView;
    public TitleView TitleView;

    private MainMenuCameraView ViewState = MainMenuCameraView.Title;
    private MainMenuView CurrentView;
    private MainMenuView GetView(MainMenuCameraView viewState)
    {
        switch (viewState)
        {
            case MainMenuCameraView.Scout:
                return ScoutView;
            case MainMenuCameraView.Supply:
                return FinanceAndSupplyView;
            default:
                return TitleView;
        }
    }

    private bool transitioning, onMainMenu = true;
    private const float transitionDuration = 1f;
    private const string DefaultRadioButtonName = "default";
    private const string RadioTagPostfix = "radio";
    private float transitionTime = 0f;
    private LerpContext cameraLerp;
    private string lastPlayerName;
    private string[] savedPlayerNames;

    internal NewGameChoices NewGameChoices;

	// Use this for initialization
	void Start ()
    {
        CurrentView = TitleView;
        FinanceAndSupplyView.LogoRight = UnityEngine.Screen.width / 2;
        FinanceAndSupplyView.LogoBottom = UnityEngine.Screen.height / 2;
        ScoutView.LogoRight = (int)(UnityEngine.Screen.width * .6666f);
        ScoutView.LogoBottom = (int)(UnityEngine.Screen.height * .6666f);

        cameraLerp.Seed(Camera.main.transform, null);
        cameraLerp.Duration = transitionDuration;

        TitleView.CanvasParent.gameObject.SetActive(true);
        FinanceAndSupplyView.CanvasParent.gameObject.SetActive(false);

        ScoutView.ToggleScoutMode(false);
        SetSelectedLocation(null);

        NewGameChoices.ChosenFinancing = BackerFinancing.Government;
        NewGameChoices.ChosenPlayerTraining = Perk.Athlete;
        NewGameChoices.RecalculateFunds();
        FinanceAndSupplyView.RefreshFunds(NewGameChoices);

        //if we start here from the escape menu, time is paused
        Time.timeScale = 1f;
        Cursor.visible = true;
        Cursor.lockState = CursorLockMode.None;

        loadSavedPlayers();
    }

    private void loadSavedPlayers()
    {
        try
        {
            lastPlayerName = PersistentDataManager.GetLastPlayedPlayerName();
            savedPlayerNames = PersistentDataManager.GetPlayerNames();

            if (!String.IsNullOrEmpty(lastPlayerName))
            {
                LoadButton.interactable = true;
                LoadButton.transform.GetChild(0).GetComponent<Text>().text = "LOAD GAME as " + lastPlayerName;
            }

        }
        catch (Exception e){
            UnityEngine.Debug.LogError(e.ToString());
        }
    }

    private GameObject[] defaultQuickstartClones = new GameObject[2];

    private void InitializeRadioButtons()
    {
        foreach (NewGameRadioButtons r in Enum.GetValues(typeof(NewGameRadioButtons)))
        {
            foreach (GameObject g in GameObject.FindGameObjectsWithTag(r.ToString() + RadioTagPostfix))
            {
                if (g.name == DefaultRadioButtonName)
                {
                    this.activeRadioTransform[r] = g.transform;
                    if (defaultQuickstartClones[(int)r] == null)
                    {
                        defaultQuickstartClones[(int)r] = GameObject.Instantiate(g);
                        defaultQuickstartClones[(int)r].transform.SetParent(QuickstartTrainingEquipmentRow);
                        //remove the checkbox
                        defaultQuickstartClones[(int)r].transform.GetChild(0).gameObject.SetActive(false);
                        defaultQuickstartClones[(int)r].tag = "Untagged";
                    }
                }
                else
                {
                    g.transform.GetChild(0).gameObject.SetActive(false);
                }
            }
        }
    }
    
    void Update() {
        if (ViewState == MainMenuCameraView.Scout)
        {
            HandleScoutInput();
        }
	}

    private BaseLocation hoverLocation;

    private void HandleScoutInput()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;
        if (NewGameChoices.ChosenLocation == null && Physics.Raycast(ray, out hit))
        {
            hoverLocation = new BaseLocation()
            {
                Region = GeoExtensions.ParseRegion(hit.collider.name),
                LatLong = LatLong.FromPointOnUnitSphere(ScoutView.ScoutOrreyHorizontal.transform.InverseTransformPoint(hit.point))
            };
            ScoutView.FillScoutInfo(hoverLocation.Region, hoverLocation.LatLong);
            ScoutView.ScoutCursor.position = hit.point;
            ScoutView.ScoutCursor.rotation = Quaternion.LookRotation(hit.normal);
        }

        if (Input.GetMouseButtonDown(0))
        {
            SetSelectedLocation(hoverLocation);
        }
        else if (Input.GetMouseButtonDown(1))
        {
            SetSelectedLocation(null);
        }
        else
        {
            float xDelta = CrossPlatformInputManager.GetAxis("Horizontal");
            float yDelta = CrossPlatformInputManager.GetAxis("Vertical");

            if (xDelta != 0f)
                ScoutView.ScoutOrreyHorizontal.transform.Rotate(Vector3.up, xDelta, Space.Self);

            if (yDelta != 0f)
                ScoutView.ScoutOrreyVertical.transform.Rotate(Vector3.forward, -yDelta, Space.Self);
        }
    }

    private void SetSelectedLocation(BaseLocation loc)
    {
        NewGameChoices.ChosenLocation = loc;
        ScoutView.ClaimHomesteadButton.gameObject.SetActive(loc != null);
        ScoutView.SelectLocationButton.gameObject.SetActive(loc == null);
        ScoutView.ScoutCursor.transform.SetParent(loc == null ? null : ScoutView.ScoutOrreyHorizontal);
    }

    public void ChangeViewInt(int newView)
    {
        ChangeView((MainMenuCameraView)newView);
    }

    public void ChangeView(MainMenuCameraView newView)
    {
        if (!transitioning)
        {
            transitioning = !transitioning;

            MainMenuView fromView = CurrentView;
            MainMenuView toView = GetView(newView);

            cameraLerp.Seed(fromView.CameraAnchor, toView.CameraAnchor);

            fromView.BeforeTransitionAway();
            ToggleLogoAndCamera(fromView, toView, () => {
                CurrentView = toView;
                CurrentView.AfterTransitionTo();

                ViewState = newView;

                if (newView == MainMenuCameraView.Supply)
                {
                    //unselect all radio buttons
                    InitializeRadioButtons();

                    QuickstartBackdrop.gameObject.SetActive(true);
                }
            });
        }
    }

    private void ToggleLogoAndCamera(MainMenuView fromView, MainMenuView toView, Action onFinishTransition)
    {
        transitionTime = 0f;
        StartCoroutine(LogoCameraChange(fromView.LogoBottom, fromView.LogoRight, toView.LogoBottom, toView.LogoRight, onFinishTransition));
    }

    private IEnumerator LogoCameraChange(float startBottom, float startRight, int endBottom, int endRight, Action onFinishTransition)
    {
        while(transitioning)
        {
            transitionTime += Time.deltaTime;

            cameraLerp.Tick(Camera.main.transform);

            if (transitionTime > transitionDuration)
            {
                SetBottomRight(BigLogo, endBottom, endRight);
                transitioning = false;
                transitionTime = 0f;
                onMainMenu = !onMainMenu;
                onFinishTransition();
            }
            else
            {
                float lerpAmt = transitionTime / transitionDuration;
                SetBottomRight(BigLogo, (int)Mathf.Lerp(startBottom, endBottom, lerpAmt), (int)Mathf.Lerp(startRight, endRight, lerpAmt) );

                yield return null;
            }
        }
    }

    private void SetBottomRight(Image bigLogo, int bottom, int right)
    {
        bigLogo.rectTransform.offsetMax = new Vector2(-right, 0f);
        bigLogo.rectTransform.offsetMin = new Vector2(0f, bottom);
    }

    public void QuitGame()
    {
        Application.Quit();
    }

    public void LaunchGame()
    {
        PersistentDataManager.StartNewGame(NewGameChoices);
        UnityEngine.SceneManagement.SceneManager.LoadScene("main", UnityEngine.SceneManagement.LoadSceneMode.Single);
    }

    public void StartQuickstart()
    {
#warning todo: make sure quickstart at quickstart equipment/training
        LaunchGame();
    }

    public void StartCustomize()
    {
        QuickstartBackdrop.gameObject.SetActive(false);
    }

    private enum NewGameRadioButtons { financing, training }

    private Dictionary<NewGameRadioButtons, Transform> activeRadioTransform = new Dictionary<NewGameRadioButtons, Transform>();

    private int currentlySelectedTrainingIndex;
    public void SelectTraining(int trainingIndex)
    {
        OnRadioSelect(NewGameRadioButtons.training);
        NewGameChoices.ChosenPlayerTraining = (Perk)trainingIndex;
        NewGameChoices.RecalculateFunds();
        FinanceAndSupplyView.RefreshFunds(NewGameChoices);
    }

    public void SelectFinancing(int financeIndex)
    {
        OnRadioSelect(NewGameRadioButtons.financing);
        NewGameChoices.ChosenFinancing = (BackerFinancing)financeIndex;
        NewGameChoices.RecalculateFunds();
        FinanceAndSupplyView.RefreshFunds(NewGameChoices);
    }

    private void OnRadioSelect(NewGameRadioButtons radioGroup)
    {
        var thisT = EventSystem.current.currentSelectedGameObject.transform;

        if (thisT.CompareTag("Untagged"))
            return;

        if (this.activeRadioTransform.ContainsKey(radioGroup))
        {
            this.activeRadioTransform[radioGroup].GetChild(0).gameObject.SetActive(false);
        }
        this.activeRadioTransform[radioGroup] = thisT;

        thisT.GetChild(0).gameObject.SetActive(true);
    }

    public void LoadLastGame()
    {
        if (!String.IsNullOrEmpty(lastPlayerName))
        {
            GameObject g = new GameObject("loadBridge");
            LoadGameBridge loadScript = g.AddComponent<LoadGameBridge>();
            loadScript.playerNameToLoad = lastPlayerName;
            UnityEngine.SceneManagement.SceneManager.LoadScene("main", UnityEngine.SceneManagement.LoadSceneMode.Single);
        }
    }

    private struct LerpContext
    {
        public Vector3 FromPosition, ToPosition;
        public Quaternion FromRotation, ToRotation;
        public float Duration;
        private float Time;
        public bool Done;

        public void Seed(Transform from, Transform to)
        {
            FromPosition = from.position;
            FromRotation = Quaternion.LookRotation(from.forward, from.up);
            if (to != null)
            {
                ToPosition = to.position;
                ToRotation = Quaternion.LookRotation(to.forward, to.up);
            }
        }

        public void Reverse()
        {
            var newToPos = FromPosition;
            var newToRot = FromRotation;
            FromPosition = ToPosition;
            FromRotation = ToRotation;
            ToPosition = newToPos;
            ToRotation = newToRot;
            Done = false;
            Time = 0f;
        }

        public void Tick(Transform transform)
        {
            this.Time += UnityEngine.Time.deltaTime;
            if (this.Time > this.Duration)
            {
                transform.position = ToPosition; 
                transform.rotation = ToRotation;
                Done = true;
            }
            else
            {
                transform.position = Vector3.Lerp(FromPosition, ToPosition, Time / Duration);
                transform.rotation = Quaternion.Lerp(FromRotation, ToRotation, Time / Duration);
            }
        }
    }
}
