using UnityEngine;
using KeepCoding;
using System.Collections;
using System.Linq;
using System.Collections.Generic;
using RNG = UnityEngine.Random;
using KModkit;
using System;
using System.Text.RegularExpressions;

public class ClockworksScript : ModuleScript {

    public KMBombInfo bomb;
    public Transform arrow;
    public MeshRenderer[] playerpips;
    public MeshRenderer arrowColor;
    public Material[] colorMats;
    public Material deadMat;

    public KMSelectable[] cardChoices;

    //The south-west player is 0, then go clockwise
    private int pointedPlayer = 0;
    private bool[] livings = new bool[6] { true, true, true, true, true, true };
    private bool[] hasSpecial = new bool[6] { true, true, true, true, true, true };
    private Personality[] personalities = new Personality[5];
    private PlayerColor[] colors;
    private bool isUserInputPossible = true;
    private char[] C = new char[5];

    private string[] intToPlace = new string[6] { "south-west", "north-west", "north", "north-east", "south-east", "south" };

    private Intention?[] intentions = new Intention?[6];

    public GameObject[] opponentCardsGOs;
    public SpriteRenderer[] opponentCardsSprites;
    public SpriteRenderer[] playerCardSprites;
    public GameObject specialPlayerCard;

    public Sprite[] cardSpritesArray; //Use the dictionnary below instead
    public Dictionary<PlayerColor, Sprite[]> playerToCards;

    private bool isCyanProtected = false;
    private bool isGreenSkipped = false;

    public KMColorblindMode colorblindMode;
    public TextMesh[] cbTexts;
    public GameObject[] cbGOs;

    private int L { get { return livings.Count(a => a); } }
    private int deadPlayers { get { return 6 - L; } }
    private bool tpColorblind = false;
    private bool ShowColorblind { get { return colorblindMode.ColorblindModeActive || tpColorblind; } }

    private Dictionary<char[], Personality> characterToPersonalityMap = new Dictionary<char[], Personality>()
    {
        { new char[] {'1','F','N'}, Personality.Toxic },
        { new char[] {'0','H','Z'}, Personality.HalfDead },
        { new char[] {'9','A','X'}, Personality.Calculating },
        { new char[] {'3','B','T'}, Personality.Empathetic },
        { new char[] {'I','Q','W'}, Personality.Apprehensive },
        { new char[] {'7','C','P'}, Personality.TimeTraveller },
        { new char[] {'6','G','M'}, Personality.Traumatised },
        { new char[] {'R','S','U'}, Personality.Copycat },
        { new char[] {'2','L','V'}, Personality.Seer },
        { new char[] {'4','8','E'}, Personality.Strange },
        { new char[] {'5','D','K'}, Personality.Occultist },
        { new char[] {'J'}, Personality.Paranoiac },
    };

    void Start() {
        playerToCards = new Dictionary<PlayerColor, Sprite[]>()
        {
            {PlayerColor.Blue, new Sprite[] {cardSpritesArray[0],cardSpritesArray[6],cardSpritesArray[12]} },
            {PlayerColor.Cyan, new Sprite[] {cardSpritesArray[1],cardSpritesArray[7],cardSpritesArray[13]} },
            {PlayerColor.Green, new Sprite[] {cardSpritesArray[2],cardSpritesArray[8],cardSpritesArray[14]} },
            {PlayerColor.Orange, new Sprite[] {cardSpritesArray[3],cardSpritesArray[9],cardSpritesArray[15]} },
            {PlayerColor.Red, new Sprite[] {cardSpritesArray[4], cardSpritesArray[10], cardSpritesArray[16] } },
            {PlayerColor.Yellow, new Sprite[] {cardSpritesArray[5],cardSpritesArray[11],cardSpritesArray[17]} },
        };
        opponentCardsGOs.ForEach(go => go.SetActive(false));
        cardChoices.Assign(onInteract: PlayCard);
        pointedPlayer = RNG.Range(0, 6);
        Log("Starting by having player {0} currently pointed", intToPlace[pointedPlayer]);
        arrow.localRotation = Quaternion.Euler(0f, (60f * pointedPlayer) - 120f, 0f);
        colors = (PlayerColor[])Enum.GetValues(typeof(PlayerColor)).Shuffle();
        for (int i = 0; i < playerpips.Length; i++)
        {
            playerpips[i].material = colorMats[(int)colors[i]];
        }
        for (int i = 0; i < playerCardSprites.Length; i++)
        {
            playerCardSprites[i].sprite = playerToCards[colors.Last()][i];
        }
        arrowColor.material = colorMats[(int)colors.Last()];
        for (int i = 0; i < intToPlace.Length; i++) Log("Player {0} has color {1}.", intToPlace[i], colors[i]);
        ToggleColorblind();
        SetPesonalities();
        PrepOpponents();
    }

    private void PrepOpponents()
    {
        for (int i = 0; i < personalities.Length; i++)
        {
            if (!livings[i])
            {
                intentions[i] = null;
                continue;
            }
            int v = i;
            Intention playingCard;
            Personality p = personalities[v];
            while (p == Personality.Copycat || p == Personality.Seer)
            {
                if (p == Personality.Copycat) //Right=CCW
                {
                    if (v == 0)
                    {
                        p = Personality.Traumatised;
                        break;
                    }
                    else
                        v--;
                }
                else //Left=CW
                {
                    if (v == 4)
                    {
                        p = Personality.Paranoiac;
                        break;
                    }
                    else
                        v++;
                }
                p = personalities[v];
            }
            switch (p)
            {
                case Personality.Toxic:
                    playingCard = (hasSpecial[i] && (ToAN(C[i]) % 5 + 2) == L) ? Intention.Special : Intention.One;
                    break;
                case Personality.HalfDead:
                    playingCard = (hasSpecial[i] && (ToAN(C[i]) % 5 + 2) == L) ? Intention.Special : Intention.Zero;
                    break;
                case Personality.Calculating:
                    playingCard = ThreatManagement(i, SimulateTurn((int)Math.Floor(L / 2f)) == i);
                    break;
                case Personality.Empathetic:
                    playingCard = ThreatManagement(i, i == (pointedPlayer + 1) % 6 || i == Helper.Modulo(pointedPlayer - 1, 6));
                    break;
                case Personality.Apprehensive:
                    playingCard = ThreatManagement(i, i == SimulateTurn(ToAN(C[i]) % 6 + 1));
                    break;
                case Personality.TimeTraveller:
                    playingCard = ThreatManagement(i, i == SimulateTurn(ToAN(C[i]) % 6 + 1, true));
                    break;
                case Personality.Traumatised:
                    playingCard = ThreatManagement(i, i == pointedPlayer);
                    break;
                case Personality.Strange:
                    playingCard = ThreatManagement(i, deadPlayers % 2 == 0);
                    break;
                case Personality.Occultist:
                    playingCard = ThreatManagement(i, deadPlayers % 2 == 1);
                    break;
                case Personality.Paranoiac:
                    playingCard = ThreatManagement(i, true);
                    break;
                default: //Copycat and Seer should never appear in this switch case
                    throw new ArgumentException(p.ToString());
            }
            intentions[i] = playingCard;
            Log("Player {0} will play {1}", intToPlace[i], intentions[i]);
        }
    }

    private Intention ThreatManagement(int player, bool isThreatened)
    {
        if (Helper.IsBetween(C[player], 'A', 'M') || Helper.IsBetween(C[player], '0', '4')) //Active
        {
            if (isThreatened)
            {
                if (hasSpecial[player] && (ToAN(C[player]) % 5) == deadPlayers) return Intention.Special;
                return Intention.One;
            }
            return Intention.Zero;
        }
        //Passive
        if (isThreatened)
        {
            if (hasSpecial[player] && (ToAN(C[player]) % 5) == deadPlayers) return Intention.Special;
            return Intention.Zero;
        }
        return Intention.One;
    }

    private int SimulateTurn(int numberOfTurns, bool isCounter = false)
    {
        int tmpPoint = pointedPlayer;
        for (int i = 0; i < numberOfTurns; i++)
        {
            tmpPoint = Helper.Modulo((tmpPoint + (isCounter ? -1 : 1)), livings.Length);
            while (!livings[tmpPoint]) tmpPoint = Helper.Modulo(tmpPoint + (isCounter ? -1 : 1), livings.Length);
        }
        return tmpPoint;
    }

    private void PlayCard(int choice)
    {
        if (!isUserInputPossible) return;
        isUserInputPossible = false;
        intentions[intentions.Length - 1] = (Intention)choice;
        Log("You chose the {0} card.", intentions.Last());
        if (choice == 2)
        {
            hasSpecial[hasSpecial.Length - 1] = false;
            specialPlayerCard.SetActive(false);
        }
        StartCoroutine(PlayRound());
    }

    private IEnumerator PlayRound()
    {
        int counter = CalculateSpins();
        Log("Rotating {0} times clockwise.", counter);
        yield return ShowOpponentCards();
        yield return Rotate(counter);
        isGreenSkipped = false;
        if (pointedPlayer == 5)
        {
            if (isCyanProtected && colors.Last() == PlayerColor.Cyan)
                Log("You are protected.");
            else
            {
                PlaySound("whomp");
                Log("The arrow is pointing at you ! Strike.");
                Strike();
            }
            opponentCardsGOs.ForEach(go => go.SetActive(false));
        }
        else
        {
            if (isCyanProtected && colors[pointedPlayer] == PlayerColor.Cyan)
            {
                Log("Cyan is protected.");
                opponentCardsGOs.ForEach(go => go.SetActive(false));
            }
            else
            {
                livings[pointedPlayer] = false;
                playerpips[pointedPlayer].material = deadMat;
                if (ShowColorblind) cbTexts[pointedPlayer].text = "X";
                PlaySound("whomp");
                Log("Eliminating player {0}.", intToPlace[pointedPlayer]);
                opponentCardsGOs.ForEach(go => go.SetActive(false));
                if (livings.Count(l => l) == 1)
                {
                    Log("You're the only player standing. Solve!");
                    Solve();
                    yield return new WaitForSecondsRealtime(1f);
                    PlaySound("win");
                    StartCoroutine(Shrink());
                    isUserInputPossible = true;
                    yield break;
                }
                else
                {
                    yield return new WaitForSecondsRealtime(1f);
                    yield return Rotate(1);
                }

            }
        }
        isCyanProtected = false;
        PrepOpponents();
        isUserInputPossible = true;
    }

    private IEnumerator ShowOpponentCards()
    {
        for (int i = 0; i < personalities.Length; i++)
        {
            if (!livings[i]) continue;
            opponentCardsSprites[i].sprite = playerToCards[colors[i]][(int)intentions[i]];
            opponentCardsGOs[i].SetActive(true);
            PlaySound("card");
            yield return new WaitForSecondsRealtime(.25f);
        }
    }

    private void SetPesonalities()
    {
        int lookAtCharacter = bomb.GetSerialNumber().Take(2).Select(x => Helper.Alphanumeric.IndexOf(x)).Sum() % 6;
        for (int i = 0; i < personalities.Length; i++)
        {
            C[i] = bomb.GetSerialNumber()[lookAtCharacter];
            personalities[i] = characterToPersonalityMap.Where(x => x.Key.Contains(bomb.GetSerialNumber()[lookAtCharacter])).First().Value;
            Log("Player {0} gets character {1}, which gives them the personality {2}-{3}.", intToPlace[i], C[i], personalities[i], (Helper.IsBetween(C[i], 'A', 'M') || Helper.IsBetween(C[i], '0', '4'))?"Active":"Passive");
            lookAtCharacter = (lookAtCharacter + 1) % 6;
        }
    }

    private IEnumerator Rotate(int spinNumber)
    {
        yield return StartCoroutine(Rotating(spinNumber));
    }

    private IEnumerator Rotating(int spinNumber)
    {
        for (int i = 0; i < Math.Abs(spinNumber); i++)
        {
            int numberOfTicks = spinNumber < 0 ? -1 : 1;
            while (!livings[Helper.Modulo(pointedPlayer + numberOfTicks, livings.Length)] || (isGreenSkipped && colors[Helper.Modulo(pointedPlayer + numberOfTicks, livings.Length)] == PlayerColor.Green)) numberOfTicks += spinNumber < 0 ? -1 : 1;
            pointedPlayer = Helper.Modulo(pointedPlayer + numberOfTicks, livings.Length);
            yield return RotateObject(60 * numberOfTicks);
        }
    }

    IEnumerator RotateObject(int howMuchToRotate)
    {
        float startRotation = arrow.localRotation.eulerAngles.y;
        float endRotation = Helper.Modulo(startRotation + howMuchToRotate, 360);
        float cumul = 0f;
        while (cumul < Math.Abs(howMuchToRotate))
        {
            float tmp = howMuchToRotate * Time.deltaTime;
            arrow.Rotate(0f, tmp, 0f, Space.Self);
            cumul += Math.Abs(tmp);
            yield return null;
        }
        PlaySound("tick");
        arrow.localRotation = Quaternion.Euler(0f, endRotation, 0f);
    }

    IEnumerator Shrink()
    {
        while (arrow.localScale.x >= 0)
        {
            float t = Time.deltaTime;
            arrow.localScale -= new Vector3(t, t, t);
            arrow.Rotate(0f, t*360f, 0f, Space.Self);
            yield return null;
        }
        Destroy(arrow.gameObject);
    }

    private int CalculateSpins(bool autoSolving = false)
    {
        bool isOrangeDoubling = false;
        bool isYellowFlipping = false;
        int counter = 0;
        int yellowFlip = 0;
        Intention?[] checkingIntentions = autoSolving ? intentions.Take(intentions.Length-1).ToArray() : intentions;
        counter += checkingIntentions.Count(i => i == Intention.One);
        for (int i = 0; i < checkingIntentions.Length; i++)
        {
            if (!livings[i]) continue;
            if (checkingIntentions[i] == Intention.Special)
            {
                switch (colors[i])
                {
                    case PlayerColor.Red:
                        counter += 2;
                        break;
                    case PlayerColor.Blue:
                        counter--;
                        break;
                    case PlayerColor.Cyan:
                        isCyanProtected = true;
                        break;
                    case PlayerColor.Yellow:
                        isYellowFlipping = true;
                        int totalSteps = 3;
                        for (int s = 1; s < 3; s++)
                        {
                            if (!livings[(pointedPlayer + s) % livings.Length]) totalSteps--;
                        }
                        yellowFlip = totalSteps;
                        break;
                    case PlayerColor.Orange:
                        if (deadPlayers <= 3) isOrangeDoubling = true;
                        break;
                    case PlayerColor.Green:
                        if (deadPlayers <= 3) isGreenSkipped = true;
                        break;
                }
                if(!autoSolving) hasSpecial[i] = false;
            }
        }
        if (isOrangeDoubling)
        {
            counter *= 2;
            if (isYellowFlipping)
            {
                int flipBack = 3;
                for (int s = 4; s < 6; s++)
                {
                    if (!livings[(pointedPlayer + s) % livings.Length]) flipBack--;
                }
                counter += flipBack;
            }
        }
            
        counter += yellowFlip;
        return counter;
    }

    private void ToggleColorblind()
    {
        if (!ShowColorblind) cbGOs.ForEach(t => t.SetActive(false));
        else
        {
            cbGOs.ForEach(t => t.SetActive(true));
            for (int i = 0; i < cbTexts.Length; i++)
            {
                if (livings[i]) cbTexts[i].text = colors[i].ToString().First().ToString();
                else cbTexts[i].text = "X";
            }
        }
    }

    private int ToAN(char c)
    {
        return Helper.Alphanumeric.IndexOf(c);
    }

#pragma warning disable 414
    private readonly string TwitchHelpMessage = @"[!{0} play 0/1/s/zero/one/special/spe] to play either the 0, 1 or Special card. [!{0} colorblind] to toggle colorblind mode.";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string command)
    {
        command = command.Trim();
        string[] commands = command.Split(" ");
        KMSelectable card = null;
        if (Regex.IsMatch(command, @"^play\s+(0|1|s|zero|one|special|spe)$", RegexOptions.IgnoreCase))
        {
            switch (commands.Last().ToLower())
            {
                case "s": case "special": case "spe":
                    if (hasSpecial.Last()) card = cardChoices[2];
                    break;
                case "0": case "zero":
                    card = cardChoices[0];
                    break;
                case "1": case "one":
                    card = cardChoices[1];
                    break;
            }
            if (card != null)
            {
                yield return null;
                yield return new WaitUntil(() => isUserInputPossible);
                card.OnInteract();
            }
        }
        if (command.Equals("colorblind", StringComparison.InvariantCultureIgnoreCase)){
            yield return null;
            tpColorblind = !tpColorblind;
            ToggleColorblind();
        }
    }
    private IEnumerator TwitchHandleForcedSolve()
    {
        yield return new WaitUntil(() => isUserInputPossible);
        while (!IsSolved)
        {
            int c = CalculateSpins(true);
            int p = pointedPlayer;
            for (int i = 0; i < Math.Abs(c); i++)
            {
                int numberOfTicks = c < 0 ? -1 : 1;
                while (!livings[Helper.Modulo(p + numberOfTicks, livings.Length)] || (isGreenSkipped && colors[Helper.Modulo(p + numberOfTicks, livings.Length)] == PlayerColor.Green)) numberOfTicks += c < 0 ? -1 : 1;
                p = Helper.Modulo(p + numberOfTicks, livings.Length);
            }
            Log(p);
            if (p == 5) cardChoices[1].OnInteract();
            else cardChoices[0].OnInteract();
            yield return new WaitUntil(() => isUserInputPossible);
        }
    }
}
