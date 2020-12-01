using System.Collections;
using System.Collections.Generic;
using Unity.MLAgents;
using UnityEngine;
using UnityEngine.Serialization;

[System.Serializable]
public class RectState
{
    public int playerIndex;
    [FormerlySerializedAs("agentRB")]
    public Rigidbody agentRb;
    public Vector3 startingPos;
    public RectAgent agentScript;
    public float ballPosReward;
    public bool isServing = false;
}

public class RectArea : MonoBehaviour
{
    public enum GamePhase
    {
        Start = 0,
        Play = 1
    }
    public GameObject ball;
    public GamePhase phase;
    [FormerlySerializedAs("ballRB")]
    [HideInInspector]
    public Rigidbody ballRb;
    // public GameObject ground;
    // public GameObject centerPitch;
    BallController m_BallController;
    public List<RectState> playerStates = new List<RectState>();
    [HideInInspector]
    public Vector3 ballStartingPos;
    public GameObject goalTextUI;
    [HideInInspector]
    public bool canResetBall;
    public int bluePlayerTurn;
    public int yellowPlayerTurn;
    public Vector3 ballVelocity;

    RectAgent.RectTeam prevScoredTeam;

    public RectAgent.RectTeam prevTouchedTeam;

    EnvironmentParameters m_ResetParams;


    public float motionTimer = 0f;
    public bool timerRunning = false;

    void Update()
    {
        if( timerRunning )
        {
            motionTimer += Time.deltaTime;

            if( ballRb.velocity.magnitude > Mathf.Epsilon )
            {
                motionTimer = 0f;
                timerRunning = false;
            }

            if( motionTimer > 5f )
            {
                this.GoalTouched(this.prevTouchedTeam);
                motionTimer = 0f;
                timerRunning = false;
            }

        }
        else
        {
            timerRunning = true;
        }
    }

    void Awake()
    {
        canResetBall = true;
        if (goalTextUI) { goalTextUI.SetActive(false); }
        ballRb = ball.GetComponent<Rigidbody>();
        m_BallController = ball.GetComponent<BallController>();
        m_BallController.area = this;
        ballStartingPos = ball.transform.position;

        System.Random rnd = new System.Random();
        int rndIndex = rnd.Next(2);
        // First team to serve is randomly selected
        prevScoredTeam = (RectAgent.RectTeam) rndIndex;
        prevTouchedTeam = prevScoredTeam;
        bluePlayerTurn = 0;
        yellowPlayerTurn = 0;
        m_ResetParams = Academy.Instance.EnvironmentParameters;

        ballRb.velocity = new Vector3(0f, 0f, 0f);
        ballRb.useGravity = false;

        // The ball should be parented to a random agent of the team that scored until
        // it is served.

        var playerCount = 0;

        // Run some setup for each agent.

        foreach (var ps in playerStates)
        {
            if(ps.agentScript.team == prevScoredTeam)
            {
                ref int playerTurn = ref this.GetPlayerTurnByTeam( ps.agentScript.team );
                if(playerCount == playerTurn)
                {
                    ball.transform.SetParent(ps.agentScript.gameObject.transform);
                    ball.transform.localPosition = new Vector3(1f, 0f, 0f);
                    ps.agentScript.isServing = true;
                    playerTurn = (playerTurn==0) ? 1 : 0;
                    ps.playerIndex = 0;
                }
                else
                {
                    ps.agentScript.isServing = false;
                    playerCount++;
                    ps.playerIndex = 1;
                }
            }
            else
            {

                ps.agentScript.isServing = false;
            }
        }

        ballRb.angularVelocity = Vector3.zero;

    }

    IEnumerator ShowGoalUI()
    {
        if (goalTextUI) goalTextUI.SetActive(true);
        yield return new WaitForSeconds(.25f);
        if (goalTextUI) goalTextUI.SetActive(false);
    }

    public void GoalTouched(RectAgent.RectTeam scoredTeam)
    {
        GiveReward(scoredTeam);
    }

    // calculate who fouled
    public void OutOfBounds(RectAgent.RectTeam fouledTeam)
    {
        if( fouledTeam == RectAgent.RectTeam.Blue)
        {
            GiveReward(RectAgent.RectTeam.Yellow);
        }
        else
        {
            GiveReward(RectAgent.RectTeam.Blue);
        }
    }

    public void GiveReward(RectAgent.RectTeam scoredTeam)
    {
        foreach (var ps in playerStates)
        {
            if (ps.agentScript.team == scoredTeam)
            {
                ps.agentScript.AddReward(1 + ps.agentScript.timePenalty);
            }
            else
            {
                ps.agentScript.AddReward(-1);
            }
            ps.agentScript.EndEpisode();  //all agents need to be reset
        }
        // set prevScoredTeam, prevTouchedTeam
        prevScoredTeam = scoredTeam;
        prevTouchedTeam = scoredTeam;
    }

    public void ResetBall()
    {
        
        ballRb.velocity = new Vector3(0f, 0f, 0f);
        ballRb.useGravity = false;
        // The ball should be parented to a random agent of the team that scored until
        // it is served.

        // The ball resets towards whichever team scored
        // ballRb.velocity = new Vector3( 0f, 0f, 1f * (1f - (float)scoredTeam) - 1f * (float)scoredTeam );

        foreach (var ps in playerStates)
        {
            if(ps.agentScript.team == prevScoredTeam)
            {
                ref int playerTurn = ref this.GetPlayerTurnByTeam( ps.agentScript.team );
                if( ps.playerIndex == playerTurn)
                {
                    //Debug.Log("player name :" + ps.agentScript.gameObject.name );
                    //Debug.Log("player index :" + ps.playerIndex );
                    //Debug.Log("player turn: "  + playerTurn );
                    ball.transform.SetParent(ps.agentScript.gameObject.transform);
                    ball.transform.localPosition = new Vector3(1f, 0.0f, 0f);
                    ps.agentScript.isServing = true;
                    
                }
                else
                {
                    ps.isServing = false;
                }
            }
            else
            {
                ps.isServing = false;
            }
        }
        //  if the ball was never reset
        //  and there was no valid agent serving
        if( ball.transform.parent == this.transform )
        {
            // the serving agent is a the first agent in the scoring team
            foreach ( var ps in playerStates )
            {
                if( ps.agentScript.team == prevScoredTeam )
                {
                    ball.transform.SetParent(ps.agentScript.gameObject.transform);
                    ball.transform.localPosition = new Vector3(1f, 0.0f, 0f);
                    ps.agentScript.isServing = true;
                    break;
                }
            }
            //Debug.Log("fuck");
        }
        ballRb.angularVelocity = Vector3.zero;
        ballRb.constraints = RigidbodyConstraints.FreezePosition | RigidbodyConstraints.FreezeRotation;
        this.phase = GamePhase.Start;
    }

    public ref int GetPlayerTurnByTeam( RectAgent.RectTeam team )
    {
        
        switch ( team )
        {
            case RectAgent.RectTeam.Blue:
                return ref this.bluePlayerTurn;
            case RectAgent.RectTeam.Yellow:
                return ref this.yellowPlayerTurn;
            default:
                // I can't think of a default value ref
                return ref this.bluePlayerTurn;
        }
    }

    public void Service( RectAgent agent )
    {
        ref int playerTurn = ref this.GetPlayerTurnByTeam( agent.team );
        if( this.phase == GamePhase.Start )
        {
            if( ball.transform.localPosition != new Vector3( 1f, 0f, 0f ))
            {
                ball.transform.localPosition = new Vector3( 1f, 0f, 0f );
            }
            ballRb.WakeUp();
            ballRb.constraints = RigidbodyConstraints.None;
            ballRb.useGravity = true;
            Vector3 localVelocity = new Vector3(20f, 0.0001f, 0f);
            //Debug.Log("team = " + agent.team.ToString() );
            //Debug.Log("localVelocity = " + localVelocity.ToString() );
            Vector3 worldVelocity = agent.transform.TransformVector(localVelocity);
            //Debug.Log(" world velocity " + worldVelocity.ToString() );
            ballRb.velocity = worldVelocity;
            
            ball.transform.SetParent( this.transform, true );
            playerTurn = ( playerTurn == 0) ? 1 : 0;
            agent.isServing = false;
            this.phase = GamePhase.Play;
        }
    }
    public void Hit( RectAgent agent )
    {
    }
}
