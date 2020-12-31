using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.MLAgents;
using Unity.MLAgents.Policies;
using Unity.MLAgents.Sensors;

public class RectAgent : Agent
{
    public enum RectTeam
    {
        Blue = 0,
        Yellow = 1
    }

    public RectTeam team;
    float m_KickPower;
    int m_PlayerIndex;
    public RectArea area;
    float m_BallTouchReward;
    public bool isServing = false;

    const float k_Power = 2000f;
    public float hitPower = 20f;
    float m_ExistentialReward;
    float m_OutOfBoundsReward;

    public float totalHitReward;

    public float m_HitReward;
    float m_LateralSpeed;
    float m_ForwardSpeed;

    [HideInInspector]
    public float timePenalty;

    [HideInInspector]
    public Rigidbody agentRb;
    RectSettings m_Settings;
    BehaviorParameters m_BehaviorParameters;
    Vector3 m_Transform;

    // Agent gameobjects for sensor data

    public GameObject friendlyAgent;
    public GameObject enemyAgent1;
    public GameObject enemyAgent2;

    // help with aiming
    public GameObject contactPoint;
    public GameObject projectedTarget;

    EnvironmentParameters m_ResetParams;

    public override void Initialize()
    {
        // fuck you mlagents team
        m_ExistentialReward = 1f / 1000000f;
        m_OutOfBoundsReward = 1f / 10000f;
        totalHitReward = 0f;
        m_HitReward = 0.05f;
        m_BehaviorParameters = gameObject.GetComponent<BehaviorParameters>();
        if (m_BehaviorParameters.TeamId == (int)RectTeam.Blue)
        {
            team = RectTeam.Blue;
            m_Transform = new Vector3(transform.position.x, 1f, transform.position.z);
        }
        else
        {
            team = RectTeam.Yellow;
            m_Transform = new Vector3(transform.position.x, 1f, transform.position.z);
        }

        m_LateralSpeed = 2.0f;
        m_ForwardSpeed = 2.0f;
        m_Settings = FindObjectOfType<RectSettings>();
        agentRb = GetComponent<Rigidbody>();
        agentRb.maxAngularVelocity = 500;

        var playerState = new RectState
        {
            agentRb = agentRb,
            startingPos = transform.position,
            agentScript = this,
        };
        area.playerStates.Add(playerState);
        m_PlayerIndex = area.playerStates.IndexOf(playerState);
        playerState.playerIndex = m_PlayerIndex;

        m_ResetParams = Academy.Instance.EnvironmentParameters;
    }

    void Update()
    {
        // Check the position for out of bounds
        if ( Mathf.Abs(transform.localPosition.x) > 15 ||
             Mathf.Abs(transform.localPosition.z) > 20 )
        {
            area.OutOfBounds(this.team);

        }

        if( Mathf.Abs(transform.localPosition.x) > 5f ||
            Mathf.Abs(transform.localPosition.z) > 10f )
        {
            m_OutOfBoundsReward = 0f;
            if( Mathf.Abs(transform.localPosition.x) > 5f)
            {
                m_OutOfBoundsReward +=  0.001f * ( Mathf.Abs(transform.localPosition.x) - 5f);
            }
            if( Mathf.Abs(transform.localPosition.z) > 10f)
            {
                m_OutOfBoundsReward += 0.001f * ( Mathf.Abs(transform.localPosition.z) - 10f);
            }
        }
    }

    public override void CollectObservations(VectorSensor sensor)
    {
        // ball position relative to agent
        Vector3 ballPosition;

        if(area.phase == RectArea.GamePhase.Start && this.isServing )
        {
            ballPosition = area.ball.transform.localPosition;
        }
        else
        {
            ballPosition = this.transform.InverseTransformPoint(area.ball.transform.position);
        }
        // Debug.Log("ballPosition for " + this.gameObject.name + " is " + ballPosition.ToString());
        sensor.AddObservation( ballPosition );
        // Debug.Log("ballPosition relative to " + this.gameObject.name + " is " + ballPosition.ToString() );
        
        // net position relative to agent
        Vector3 netPosition = this.transform.InverseTransformPoint(this.transform.parent.position);
        sensor.AddObservation( new Vector3( netPosition.x, -netPosition.y, netPosition.z ) );
        // Debug.Log("net position relative to " + this.gameObject.name + " is " + netPosition.ToString());

        // agent y rotation ( depends on team )
        Quaternion inputRotation = this.transform.localRotation;
        if ( team == RectTeam.Blue)
        {
            inputRotation *= Quaternion.Euler(0, -180, 0);
        }
        sensor.AddObservation( inputRotation );
        // Debug.Log( "agent rotation for " + this.gameObject.name + " is " + inputRotation.ToString());

        // friendlyAgent position
        Vector3 friendlyAgentPos = this.transform.InverseTransformPoint( friendlyAgent.transform.position );
        sensor.AddObservation( friendlyAgentPos.x );
        sensor.AddObservation( friendlyAgentPos.z );
        // Debug.Log(" friendlyAgent position relative to " + this.gameObject.name + " is " + friendlyAgentPos.ToString());
        // enemyAgent1 position
        Vector3 enemyAgent1Pos = this.transform.InverseTransformPoint( enemyAgent1.transform.position );
        sensor.AddObservation( enemyAgent1Pos.x );
        sensor.AddObservation( enemyAgent1Pos.z );
        // Debug.Log("enemyAgent1 position relative to " + this.gameObject.name + " is " + enemyAgent1Pos.ToString());
        // enemyAgent2 position
        Vector3 enemyAgent2Pos = this.transform.InverseTransformPoint( enemyAgent2.transform.position );
        sensor.AddObservation( enemyAgent2Pos.x );
        sensor.AddObservation( enemyAgent2Pos.z );
        // Debug.Log(" enemyAgent2 position relative to " + this.gameObject.name + " is " + enemyAgent2Pos.ToString());

    }

    public void MoveAgent(float[] act)
    {
        var dirToGo = Vector3.zero;
        var rotateDir = Vector3.zero;

        m_KickPower = 0f;

        var forwardAxis = (int)act[0];
        var rightAxis = (int)act[1];
        var rotateAxis = (int)act[2];
        var service = (int)act[3];

        switch (forwardAxis)
        {
            case 1:
                dirToGo = transform.forward * m_ForwardSpeed;
                m_KickPower = 1000f;
                break;
            case 2:
                dirToGo = transform.forward * -m_ForwardSpeed;
                break;
        }

        switch (rightAxis)
        {
            case 1:
                dirToGo = transform.right * m_LateralSpeed;
                break;
            case 2:
                dirToGo = transform.right * -m_LateralSpeed;
                break;
        }

        switch (rotateAxis)
        {
            case 1:
                rotateDir = transform.up * -1f;
                break;
            case 2:
                rotateDir = transform.up * 1f;
                break;
        }

        switch (service)
        {
            case 1:
                break;
            case 2:
                if( this.isServing && area.phase == RectArea.GamePhase.Start )
                {
                    area.Service( this );
                } 
                else if( area.phase == RectArea.GamePhase.Play )
                {
                    area.Hit( this );
                }
                break;
        }

        transform.Rotate(rotateDir, Time.deltaTime * 100f);
        agentRb.AddForce(dirToGo * m_Settings.agentRunSpeed,
            ForceMode.VelocityChange);
    }

    public override void OnActionReceived(float[] vectorAction)
    {

        // Existential penalty cumulant for Generic
        // timePenalty -= m_ExistentialReward;
        timePenalty -= m_OutOfBoundsReward;
        

        MoveAgent(vectorAction);
    }

    public override void Heuristic(float[] actionsOut)
    {
        Array.Clear(actionsOut, 0, actionsOut.Length);
        //forward
        if (Input.GetKey(KeyCode.A))
        {
            actionsOut[0] = 1f;
        }
        if (Input.GetKey(KeyCode.D))
        {
            actionsOut[0] = 2f;
        }
    
        //rotate
        if (Input.GetKey(KeyCode.LeftArrow))
        {
            actionsOut[2] = 1f;
        }
        if (Input.GetKey(KeyCode.RightArrow))
        {
            actionsOut[2] = 2f;
        }
        //right
        if (Input.GetKey(KeyCode.W))
        {
            actionsOut[1] = 1f;
        }
        if (Input.GetKey(KeyCode.S))
        {
            actionsOut[1] = 2f;
        }
        if (Input.GetKey(KeyCode.Space))
        {
            actionsOut[3] = 2f;
        }
    }
    /// <summary>
    /// Used to provide a "kick" to the ball.
    /// </summary>
    void OnCollisionEnter(Collision c)
    {
        var force = k_Power * m_KickPower;
        if (c.gameObject.CompareTag("Ball"))
        {
            AddReward(.2f * m_BallTouchReward);
            var dir = c.contacts[0].point - transform.position;
            dir = dir.normalized;
            c.gameObject.GetComponent<Rigidbody>().AddForce(dir * force);
            // Debug.Log("kicked");
        }
    }

    public override void OnEpisodeBegin()
    {

        timePenalty = 0;
        m_BallTouchReward = m_ResetParams.GetWithDefault("ball_touch", 0);
        if (team == RectTeam.Yellow)
        {
            transform.rotation = Quaternion.Euler(0f, -90f, 0f);
        }
        else
        {
            transform.rotation = Quaternion.Euler(0f, 90f, 0f);
        }
        transform.position = m_Transform;
        agentRb.velocity = Vector3.zero;
        agentRb.angularVelocity = Vector3.zero;
        SetResetParameters();
    }
	
    public void SetResetParameters()
    {
        area.ResetBall();
    }
}
