﻿using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

public class RobBossEditor : EditorWindow {

	static RobBossEditor window;
	static bool painting = false;

	static int canvasID = 0;
	static bool directional = false;
	static Color color = Color.white;
	static float radius = 0.5f;
	static float blend = 0.1f;

	static string[] canvasNames = new string[0];
	static MeshCollider raycastTarget;
	static Mesh colliderMesh;
	static Renderer paintTarget;
	static Vector2 uv;

	static int w = 1024;
	static int h = 1024;
	static Texture2D canvas;
	static string canvasPath;
	static RenderTexture _renderCanvas;
	static RenderTexture renderCanvas {
		get {
			if (paintTarget == null) return null;
			if (_renderCanvas == null) {
				_renderCanvas = new RenderTexture(w, h, 0, RenderTextureFormat.ARGB32);
		    	_renderCanvas.Create();

		    	canvas = paintTarget.sharedMaterial.GetTexture(canvasNames[canvasID]) as Texture2D;
		    	if (canvas == null) {
					Graphics.Blit(Texture2D.whiteTexture, _renderCanvas);
			    }
			    else {
					canvasPath = Application.dataPath + AssetDatabase.GetAssetPath(canvas.GetInstanceID());
			    	canvasPath = canvasPath.Replace("AssetsAssets", "Assets");
					Graphics.Blit(canvas, _renderCanvas);
			    }

		    	paintTarget.sharedMaterial.SetTexture(canvasNames[canvasID], _renderCanvas);
			}
			return _renderCanvas;
		}
	}

	static Material _brushMaterial;
	static Material brushMaterial {
		get {
			if (_brushMaterial == null) {
				_brushMaterial = new Material(Shader.Find("Rob Boss/Brush"));
				_brushMaterial.SetTexture("_Brush", brushTexture);
			}
			return _brushMaterial;
		}
	}

	static Texture2D _brushTexture;
	static Texture2D brushTexture {
		get {
			if (_brushTexture == null) {
				_brushTexture = new Texture2D(32, 32, TextureFormat.ARGB32, false);
				Color[] colors = new Color[1024];
				for (int i = 0; i < 32; i++) {
					for (int j = 0; j < 32; j++) {
						float x = i*0.0625f-1;
						float y = j*0.0625f-1;
						float a = Mathf.Clamp01(1-Mathf.Sqrt(x*x+y*y));
						colors[(int)(j*32+i)] = new Color(1,1,1,a*a);
					}
				}
				_brushTexture.SetPixels(colors);
				_brushTexture.Apply();
				_brushTexture.alphaIsTransparency = true;
				_brushTexture.wrapMode = TextureWrapMode.Clamp;
			}
			return _brushTexture;
		}
		set {
			if (value != _brushTexture) {
				_brushTexture = value;
				brushMaterial.SetTexture("_Brush", _brushTexture);
			}
		}
	}

	[MenuItem ("Window/Rob Boss Painter")]
	static void Open () {
		window = EditorWindow.GetWindow(typeof(RobBossEditor)) as RobBossEditor;
		window.minSize = new Vector2(250, 360);
		window.Show();
	}

	static SceneView.OnSceneFunc onSceneFunc;
    void OnEnable () {
   		if (onSceneFunc == null) onSceneFunc = new SceneView.OnSceneFunc(OnSceneGUI);

		GameObject g = new GameObject("RobBossTarget");
		g.hideFlags = HideFlags.HideAndDontSave;
		raycastTarget = g.AddComponent<MeshCollider>();
		colliderMesh = new Mesh();
		colliderMesh.name = "RobBossColliderMesh";
		colliderMesh.hideFlags = HideFlags.HideAndDontSave;
		raycastTarget.sharedMesh = colliderMesh;

		int selectionID = EditorPrefs.GetInt("RobBoss.SelectionID", -1);
		SetPaintTarget(EditorUtility.InstanceIDToObject(selectionID) as Renderer);
		
		directional = EditorPrefs.GetInt("RobBoss.Directional", 0) == 1;
		string colorString = EditorPrefs.GetString("RobBoss.Color", "#FFFFFFFF");
		ColorUtility.TryParseHtmlString(colorString, out color);
		radius = EditorPrefs.GetFloat("RobBoss.Radius", 0.5f);
		blend = EditorPrefs.GetFloat("RobBoss.Blend", 0.1f);
    }

    void OnDisable () {
    	if (painting) SceneView.onSceneGUIDelegate -= onSceneFunc;

		DestroyImmediate(colliderMesh);
		DestroyImmediate(raycastTarget.gameObject);

		if (paintTarget != null) EditorPrefs.SetInt("RobBoss.SelectionID", paintTarget.GetInstanceID());
		EditorPrefs.SetInt("RobBoss.Directional", directional ? 1 : 0);
		EditorPrefs.SetString("RobBoss.Color", ColorUtility.ToHtmlStringRGBA(color));
		EditorPrefs.SetFloat("RobBoss.Radius", radius);
		EditorPrefs.SetFloat("RobBoss.Blend", blend);
    }

	void OnGUI () {
		EditorGUILayout.ObjectField("Paint Target", paintTarget, typeof(Renderer), true);
		canvasID = EditorGUILayout.Popup("Canvas", canvasID, canvasNames);
		directional = EditorGUILayout.Toggle("Directional", directional);
		brushTexture = EditorGUILayout.ObjectField("Brush", brushTexture, typeof(Texture2D), false) as Texture2D;
		color = EditorGUILayout.ColorField("Color", color);		
		radius = EditorGUILayout.FloatField("Radius", radius);
		blend = EditorGUILayout.FloatField("Blend", blend);

		if (!painting && GUILayout.Button("Start Painting")) {
			painting = true;
			SceneView.onSceneGUIDelegate += onSceneFunc;
		}
		else if (painting && GUILayout.Button("Stop Painting")) {
			painting = false;
			SceneView.onSceneGUIDelegate -= onSceneFunc;
			paintTarget.sharedMaterial.SetTexture(canvasNames[canvasID], canvas);
			if (_renderCanvas != null) {
				_renderCanvas.Release();
				_renderCanvas = null;
			}
		}
	}

	public static void OnSceneGUI(SceneView sceneview) {
		EventType t = Event.current.type;			
		if (painting && (t == EventType.MouseDown || t == EventType.MouseDrag) && RaycastTarget()) {
			PaintTarget();
		}
		else if (t == EventType.MouseUp) {
            GUIUtility.hotControl = 0;
    	}
	}

	void OnSelectionChange () {
		if (UpdateTarget()) Repaint();
    }

	static bool UpdateTarget() {
		if (Selection.activeGameObject == null) return false;
		Renderer r = Selection.activeGameObject.GetComponent<Renderer>();
		if (r != paintTarget) {
			SetPaintTarget(r);
			return true;
		}
		return false;
	}

	static void SetPaintTarget (Renderer r) {
		if (r == null) return;

		paintTarget = r;
		UpdateCanvasNames();
		colliderMesh.Clear();

		if (paintTarget is MeshRenderer) {
			MeshFilter f = paintTarget.GetComponent<MeshFilter>();
			if (f != null && f.sharedMesh != null) {
				colliderMesh.vertices = f.sharedMesh.vertices;
				colliderMesh.uv = f.sharedMesh.uv;
				colliderMesh.triangles = f.sharedMesh.triangles;
			}
		}
		else if (paintTarget is SpriteRenderer) {
			Sprite s = (paintTarget as SpriteRenderer).sprite;
			if (s != null) {
				colliderMesh.vertices = Array.ConvertAll(s.vertices, (v) => (Vector3)v);
				colliderMesh.uv = s.uv;
				colliderMesh.triangles = Array.ConvertAll(s.triangles, (t) => (int)t);
			}
		}

		raycastTarget.sharedMesh = colliderMesh;
		raycastTarget.transform.position = paintTarget.transform.position;
		raycastTarget.transform.rotation = paintTarget.transform.rotation;
		raycastTarget.transform.localScale = paintTarget.transform.localScale;
	}

	static void UpdateCanvasNames() {
		List<string> names = new List<string>();
		Shader shader = paintTarget.sharedMaterial.shader;
    	for (int i = 0; i < ShaderUtil.GetPropertyCount(shader); i++) {
			if (ShaderUtil.IsShaderPropertyHidden(shader, i)) continue;
    		if (ShaderUtil.GetPropertyType(shader, i) == ShaderUtil.ShaderPropertyType.TexEnv) {
    			names.Add(ShaderUtil.GetPropertyName(shader, i));
			}
    	}
		canvasNames = names.ToArray();
	}

	static bool RaycastTarget() {	
		if (raycastTarget == null) return false;

		Ray ray = HandleUtility.GUIPointToWorldRay(Event.current.mousePosition);
		RaycastHit hit;
		if (raycastTarget.Raycast(ray, out hit, Mathf.Infinity)) {
			if (directional) {
				Vector2 dir = ((hit.textureCoord - uv).normalized + Vector2.one) * 0.5f;
				color = new Color(dir.x, dir.y, 0, 1);
			}

			uv = hit.textureCoord;
			brushMaterial.SetVector("_Transform", new Vector4(uv.x, uv.y, 0, radius));
			brushMaterial.SetColor("_Color", color);

			Handles.color = color;
			Handles.DrawLine(hit.point, hit.point + hit.normal * 2);
			Handles.DrawSolidDisc(hit.point, hit.normal, radius * paintTarget.bounds.extents.y);
			HandleUtility.Repaint();

			return true;
		}
		return false;
	}

	static void PaintTarget () {
		if (paintTarget == null) return;

		Event e = Event.current;
		if (e.modifiers != EventModifiers.None) return;

		if (e.type == EventType.MouseDown) {
            GUIUtility.hotControl = GUIUtility.GetControlID(FocusType.Passive);
			Graphics.Blit(renderCanvas, renderCanvas, brushMaterial);
			e.Use();
    	}
        else if (e.type == EventType.MouseDrag) {
			Graphics.Blit(renderCanvas, renderCanvas, brushMaterial);
			e.Use();
        }
	}
}
