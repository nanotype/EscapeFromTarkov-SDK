﻿using UnityEngine;
using UnityEditor;
using UnityEngine.Playables;
using UnityEngine.Animations;
using AnimationEventSystem;
using AnimationEvent = AnimationEventSystem.AnimationEvent;
using System.Collections.Generic;
using System.Reflection;

public class StaticDataEditor : EditorWindow
{
    private AnimatorControllerStaticData staticData;
    private int eventCollectionIndex = 0;
    private int animationEventIndex = 0;
    private int eventConditionIndex = 0;

    // Temporary function list
    private readonly string[] functionNames = {
        "None",
        "Sound",
        "ThirdAction",
        "UseProp",
        "AddAmmoInChamber",
        "AddAmmoInMag",
        "Arm",
        "Cook",
        "DelAmmoChamber",
        "DelAmmoFromMag",
        "Disarm",
        "FireEnd",
        "FiringBullet",
        "FoldOff",
        "FoldOn",
        "IdleStart",
        "LauncherAppeared",
        "LauncherDisappeared",
        "MagHide",
        "MagIn",
        "MagOut",
        "MagShow",
        "MalfunctionOff",
        "ModChanged",
        "OffBoltCatch",
        "OnBoltCatch",
        "RemoveShell",
        "ShellEject",
        "WeapIn",
        "WeapOut",
        "OnBackpackDrop"
    };    
    private int selectedFunctionIndex = 0;
    private int paramType = 0;
    private readonly string[] paramName = { "None", "Int32", "Float", "String", "Boolean" };

    private bool showEventConditions = false;

    private AnimationClip animationClip;
    private PlayableGraph playableGraph;
    private AnimationClipPlayable playable;
    private bool isPlaying = false;
    private float animationTime = 0f;
    private float lastUpdateTime = 0f;
    private PreviewRenderUtility previewRenderUtility;
    private GameObject previewObject;
    private GameObject userPreviewObject;

    private Vector2 previewDir = new Vector2(120f, -20f);
    private float previewDistance = 5f;
    private Vector3 pivotPoint = Vector3.zero;
    private Light previewLight;

    [MenuItem("Custom Windows/Static Data Editor")]
    public static void ShowWindow()
    {
        GetWindow<StaticDataEditor>("Static Data Editor");
    }

    private void OnEnable()
    {
        playableGraph = PlayableGraph.Create();
        playableGraph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        previewRenderUtility = new PreviewRenderUtility();
        previewRenderUtility.cameraFieldOfView = 30f;
        previewLight = new GameObject("Preview Light").AddComponent<Light>();
        previewLight.type = LightType.Directional;
        previewLight.intensity = 1.0f;
        previewRenderUtility.AddSingleGO(previewLight.gameObject);
    }

    private void OnDisable()
    {
        playableGraph.Destroy();
        previewRenderUtility.Cleanup();
        if (previewLight != null)
        {
            DestroyImmediate(previewLight.gameObject);
        }
    }

    private void OnGUI()
    {
        // Static data editing section
        GUILayout.Label("Static Data Editor", EditorStyles.boldLabel);
        staticData = (AnimatorControllerStaticData)EditorGUILayout.ObjectField("Static Data", staticData, typeof(AnimatorControllerStaticData), false);
        eventCollectionIndex = EditorGUILayout.IntField("Events Collection Index", eventCollectionIndex);
        animationEventIndex = EditorGUILayout.IntField("Animation Event Index", animationEventIndex);
        if (staticData == null)
        {
            EditorGUILayout.HelpBox("Please assign an AnimatorControllerStaticData object.", MessageType.Warning);
        }
        else
        {
            DrawStaticDataEditor();
        }

        // Animation preview section
        GUILayout.Space(10);
        EditorGUILayout.LabelField("Animation Clip", EditorStyles.boldLabel);
        animationClip = (AnimationClip)EditorGUILayout.ObjectField("Animation Clip", animationClip, typeof(AnimationClip), false);

        EditorGUILayout.Space();

        EditorGUILayout.LabelField("Preview Object", EditorStyles.boldLabel);
        userPreviewObject = (GameObject)EditorGUILayout.ObjectField("User Preview Object", userPreviewObject, typeof(GameObject), false);
        
        if (animationClip == null)
        {
            EditorGUILayout.HelpBox("Please assign an Animation Clip.", MessageType.Warning);
            return;
        }

        if (GUILayout.Button(isPlaying ? "Stop" : "Play"))
        {
            if (isPlaying)
            {
                StopAnimation();
            }
            else
            {
                PlayAnimation();
            }
        }

        float newProgress = EditorGUILayout.Slider("Progress", animationTime / animationClip.length, 0f, 1f);
        float newAnimationTime = newProgress * animationClip.length;
        if (!isPlaying && newAnimationTime != animationTime)
        {
            animationTime = newAnimationTime;
            playable.SetTime(animationTime);
            playableGraph.Evaluate();
            Repaint();
        }

        Rect previewRect = GUILayoutUtility.GetRect(200, 200, GUILayout.ExpandWidth(true), GUILayout.ExpandHeight(true));
        previewRenderUtility.BeginPreview(previewRect, GUIStyle.none);
        HandleCameraControls(previewRect);
        previewRenderUtility.camera.nearClipPlane = 0.01f;
        previewRenderUtility.camera.farClipPlane = 1000f;
        previewLight.transform.position = previewRenderUtility.camera.transform.position;
        previewLight.transform.LookAt(pivotPoint);

        Quaternion camRotation = Quaternion.Euler(previewDir.y, previewDir.x, 0f);
        Vector3 camPosition = pivotPoint - camRotation * Vector3.forward * previewDistance;
        previewRenderUtility.camera.transform.position = camPosition;
        previewRenderUtility.camera.transform.rotation = camRotation;
        previewRenderUtility.Render();
        previewRenderUtility.EndAndDrawPreview(previewRect);

        if (isPlaying)
        {
            float currentTime = Time.realtimeSinceStartup;
            animationTime += currentTime - lastUpdateTime;
            lastUpdateTime = currentTime;
            if (animationTime > animationClip.length)
            {
                animationTime = 0f;
            }
            playable.SetTime(animationTime);
            playableGraph.Evaluate();
            Repaint();
        }
    }

    private void HandleCameraControls(Rect previewRect)
    {
        Event e = Event.current;
        if (previewRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.ScrollWheel)
            {
                previewDistance += e.delta.y * 0.05f;
                e.Use();
            }
            else if (e.type == EventType.MouseDrag)
            {
                if (e.button == 0)
                {
                    previewDir.x += e.delta.x * Mathf.Lerp(0.1f, 1f, previewDistance / 10f);
                    previewDir.y += e.delta.y * Mathf.Lerp(0.1f, 1f, previewDistance / 10f);
                    e.Use();
                }
                else if (e.button == 1)
                {
                    pivotPoint += previewRenderUtility.camera.transform.right * -e.delta.x * 0.02f * Mathf.Lerp(0.1f, 1f, previewDistance / 75f);
                    pivotPoint -= previewRenderUtility.camera.transform.up * -e.delta.y * 0.02f * Mathf.Lerp(0.1f, 1f, previewDistance / 75f);
                    e.Use();
                }
            }
        }
    }

    private void PlayAnimation()
    {
        if (previewObject != null)
        {
            DestroyImmediate(previewObject);
        }

        if (userPreviewObject != null)
        {
            previewObject = Instantiate(userPreviewObject);
            previewRenderUtility.AddSingleGO(previewObject);

            if (previewObject.GetComponent<Animator>() == null)
            {
                previewObject.AddComponent<Animator>();
            }
        }
        else
        {
            previewObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
            previewRenderUtility.AddSingleGO(previewObject);
        }

        playable = AnimationClipPlayable.Create(playableGraph, animationClip);
        var animator = previewObject.GetComponent<Animator>();
        var output = AnimationPlayableOutput.Create(playableGraph, "Animation", animator);
        output.SetSourcePlayable(playable);
        playableGraph.Play();
        isPlaying = true;
        animationTime = 0f;
        lastUpdateTime = Time.realtimeSinceStartup;
    }

    private void DrawStaticDataEditor()
    {
        var eventsCollection = GetOrCreateElement<EventsCollection>(staticData, "_stateHashToEventsCollection", eventCollectionIndex);      
        var animationEvent = GetOrCreateElement<AnimationEvent>(eventsCollection, "_animationEvents", animationEventIndex);

        // Function name dropdown
        selectedFunctionIndex = EditorGUILayout.Popup("Function Name", selectedFunctionIndex, functionNames);        

        switch (functionNames[selectedFunctionIndex])
        {
            case "Sound":
            case "ThirdAction":
            case "UseProp":
                DrawAnimationEventParameter(animationEvent);
                break;
            default:
                ResetEventParameter(animationEvent);
                break;            
        }

        // Show event conditions
        showEventConditions = EditorGUILayout.Toggle("Show Event Conditions", showEventConditions);
        if (showEventConditions)
        {
            eventConditionIndex = EditorGUILayout.IntField("Event Condition Index", eventConditionIndex);
            EnsureListSize(animationEvent.EventConditions, eventConditionIndex + 1);

            var eventCondition = animationEvent.EventConditions[eventConditionIndex];
            eventCondition.BoolValue = EditorGUILayout.Toggle("Bool Value", eventCondition.BoolValue);
            eventCondition.FloatValue = EditorGUILayout.FloatField("Float Value", eventCondition.FloatValue);
            eventCondition.IntValue = EditorGUILayout.IntField("Int Value", eventCondition.IntValue);
            eventCondition.ParameterName = EditorGUILayout.TextField("Parameter Name", eventCondition.ParameterName);
            eventCondition.ConditionParamType = (EEventConditionParamTypes)EditorGUILayout.EnumPopup("Condition Param Type", eventCondition.ConditionParamType);
            eventCondition.ConditionMode = (EEventConditionModes)EditorGUILayout.EnumPopup("Condition Mode", eventCondition.ConditionMode);
        }
        else
        {
            animationEvent.EventConditions.Clear();
        }

        // Add Event button
        if (GUILayout.Button("Add Event"))
        {
            AddOrUpdateEvent(staticData, eventsCollection, animationEvent);
            animationEvent.GetType().GetField("_functionName", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(animationEvent, functionNames[selectedFunctionIndex]);
        }
    }

    private void DrawAnimationEventParameter(AnimationEvent animationEvent)
    {
        EditorGUILayout.LabelField("Animation Event Parameter", EditorStyles.boldLabel);
        var parameter = animationEvent.Parameter;

        if (parameter == null)
        {
            parameter = new AnimationEventParameter();
            animationEvent.Parameter = parameter;
        }

        parameter.BoolParam = EditorGUILayout.Toggle("Bool Param", parameter.BoolParam);
        parameter.FloatParam = EditorGUILayout.FloatField("Float Param", parameter.FloatParam);
        parameter.IntParam = EditorGUILayout.IntField("Int Param", parameter.IntParam);
        parameter.StringParam = EditorGUILayout.TextField("String Param", parameter.StringParam);
        paramType = EditorGUILayout.Popup("Param Type", paramType, paramName);
        parameter.ParamType = (EAnimationEventParamType)paramType;
    }

    private void ResetEventParameter(AnimationEvent animationEvent)
    {
        var parameter = animationEvent.Parameter;
        if (parameter == null)
        {
            parameter = new AnimationEventParameter();
            animationEvent.Parameter = parameter;
        }

        parameter.BoolParam = false;
        parameter.FloatParam = 0;
        parameter.IntParam = 0;
        parameter.StringParam = "";
        parameter.ParamType = 0;
    }

    private T GetOrCreateElement<T>(object obj, string fieldName, int index) where T : new()
    {
        var list = obj.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance).GetValue(obj) as List<T>;
        EnsureListSize(list, index + 1);
        return list[index];
    }

    private void EnsureListSize<T>(List<T> list, int size) where T : new()
    {
        while (list.Count < size)
        {
            list.Add(new T());
        }
    }

    private void AddOrUpdateEvent(AnimatorControllerStaticData staticData, EventsCollection eventCollection, AnimationEvent animationEvent)
    {
        var animationEvents = eventCollection.GetType().GetField("_animationEvents", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(eventCollection) as List<AnimationEvent>;

        if (animationEventIndex < animationEvents.Count)
        {
            animationEvents[animationEventIndex] = animationEvent;
        }
        else
        {
            animationEvents.Add(animationEvent);
        }

        animationEvent.GetType().GetField("_time", BindingFlags.NonPublic | BindingFlags.Instance).SetValue(animationEvent, animationTime / animationClip.length);
        CallValidate(staticData);
        EditorUtility.SetDirty(staticData);
        AssetDatabase.SaveAssets();
    }

    private void CallValidate(AnimatorControllerStaticData staticData)
    {
        MethodInfo onValidateMethod = typeof(AnimatorControllerStaticData).GetMethod("OnValidate", BindingFlags.NonPublic | BindingFlags.Instance);
        if (onValidateMethod != null)
        {
            onValidateMethod.Invoke(staticData, null);
        }
    }

    private void StopAnimation()
    {
        playableGraph.Stop();
        isPlaying = false;
    }
}