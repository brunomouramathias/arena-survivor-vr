using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using ArenaSurvivor.Player;
using ArenaSurvivor.Weapons;
using ArenaSurvivor.Core;

namespace ArenaSurvivor.Editor
{
    public static class PlayerWeaponOverhaul
    {
        private const string AUDIO_DIR = "Assets/_Project/Audio";
        private const string MAT_DIR   = "Assets/_Project/Materials";
        private const string MESH_DIR  = "Assets/_Project/Models";
        private const string PREFAB_DIR = "Assets/_Project/Prefabs";
        private const string SO_DIR    = "Assets/_Project/ScriptableObjects/Weapons";

        [MenuItem("Tools/Arena/Overhaul Player & Weapons (Passo 2 e 3)", priority = 30)]
        public static void Run()
        {
            RunBatch(false);
        }

        public static void RunBatchMethod()
        {
            RunBatch(true);
        }

        public static void RunBatch(bool isBatch)
        {
            Debug.Log("=== [OVERHAUL] Iniciando reestruturação do Player, Projétil e Efeitos ===");

            Directory.CreateDirectory(AUDIO_DIR);
            Directory.CreateDirectory(MAT_DIR);
            Directory.CreateDirectory(MESH_DIR);
            Directory.CreateDirectory(PREFAB_DIR);

            // 1. Gerar arquivos de Áudio WAV procedurais
            string sfxShootPath  = $"{AUDIO_DIR}/Sfx_Shoot.wav";
            string sfxEmptyPath  = $"{AUDIO_DIR}/Sfx_Empty.wav";
            string sfxReloadPath = $"{AUDIO_DIR}/Sfx_Reload.wav";

            GenerateShootWav(sfxShootPath);
            GenerateEmptyWav(sfxEmptyPath);
            GenerateReloadWav(sfxReloadPath);
            AssetDatabase.Refresh();

            var sfxShoot  = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxShootPath);
            var sfxEmpty  = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxEmptyPath);
            var sfxReload = AssetDatabase.LoadAssetAtPath<AudioClip>(sfxReloadPath);
            Debug.Log($"[OVERHAUL] Áudios carregados: Shoot={sfxShoot != null}, Empty={sfxEmpty != null}, Reload={sfxReload != null}");

            // 2. Gerar Malha da Bala e Materiais
            var bulletMesh = CreateBulletMesh();
            AssetDatabase.CreateAsset(bulletMesh, $"{MESH_DIR}/Mesh_Bullet.asset");

            var matBullet = new Material(Shader.Find("Universal Render Pipeline/Lit"));
            matBullet.name = "Mat_Bullet_Metal";
            matBullet.SetColor("_BaseColor", new Color(0.12f, 0.12f, 0.14f, 1f)); // Preto metálico escuro
            matBullet.SetFloat("_Metallic", 0.9f);
            matBullet.SetFloat("_Smoothness", 0.8f);
            AssetDatabase.CreateAsset(matBullet, $"{MAT_DIR}/Mat_Bullet_Metal.mat");

            var matTrail = new Material(Shader.Find("Universal Render Pipeline/Particles/Unlit"));
            matTrail.name = "Mat_Bullet_Trail";
            matTrail.SetColor("_BaseColor", new Color(2.5f, 1.2f, 0.2f, 1f)); // HDR Orange/Yellow emissivo
            matTrail.SetFloat("_Surface", 1); // Transparent
            matTrail.SetFloat("_Blend", 0); // Alpha
            AssetDatabase.CreateAsset(matTrail, $"{MAT_DIR}/Mat_Bullet_Trail.mat");

            // 3. Criar MuzzleFlash.prefab
            var muzzleFlashPrefab = CreateMuzzleFlashPrefab(matTrail);

            // 4. Criar ImpactEffect.prefab
            var impactPrefab = CreateImpactPrefab(matTrail);

            // 5. Criar/Atualizar Projectile_Player.prefab
            var projectilePrefab = CreateProjectilePrefab(bulletMesh, matBullet, matTrail, impactPrefab);

            // 6. Atualizar ScriptableObjects de Armas
            UpdateWeaponDataSO("WeaponData_Pistol", projectilePrefab, muzzleFlashPrefab, sfxShoot, sfxEmpty, sfxReload);
            UpdateWeaponDataSO("WeaponData_Shotgun", projectilePrefab, muzzleFlashPrefab, sfxShoot, sfxEmpty, sfxReload);
            UpdateWeaponDataSO("WeaponData_Energy", projectilePrefab, muzzleFlashPrefab, sfxShoot, sfxEmpty, sfxReload);

            // 7. Atualizar Jogador na cena Arena (Rig, WeaponSocket, Blaster, Muzzle, Animator)
            UpdatePlayerInScene();

            AssetDatabase.SaveAssets();
            Debug.Log("=== [OVERHAUL] Concluído com sucesso! ===");

            if (isBatch) EditorApplication.Exit(0);
        }

        private static Mesh CreateBulletMesh()
        {
            var mesh = new Mesh { name = "Mesh_Bullet" };
            int segments = 16;
            float length = 0.25f;
            float radius = 0.06f;
            float bodyLength = 0.16f; // parte cilíndrica

            var vertices = new List<Vector3>();
            var normals  = new List<Vector3>();
            var uvs      = new List<Vector2>();
            var triangles = new List<int>();

            // Vértice base (centro da tampa traseira)
            vertices.Add(Vector3.zero);
            normals.Add(-Vector3.forward);
            uvs.Add(new Vector2(0.5f, 0.5f));

            // Anel base
            for (int i = 0; i < segments; i++)
            {
                float angle = (i / (float)segments) * Mathf.PI * 2f;
                float x = Mathf.Cos(angle) * radius;
                float y = Mathf.Sin(angle) * radius;
                vertices.Add(new Vector3(x, y, 0f));
                normals.Add(-Vector3.forward);
                uvs.Add(new Vector2(x / (radius * 2f) + 0.5f, y / (radius * 2f) + 0.5f));
            }
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(0);
                triangles.Add((i + 1) % segments + 1);
                triangles.Add(i + 1);
            }

            // Corpo cilíndrico e ogiva
            int rings = 6;
            int startRingIndex = vertices.Count;

            for (int r = 0; r <= rings; r++)
            {
                float t = r / (float)rings;
                float z = t * length;
                float rRadius;
                if (z <= bodyLength)
                {
                    rRadius = radius;
                }
                else
                {
                    // Ogiva cônica/arredondada
                    float ogiveT = (z - bodyLength) / (length - bodyLength);
                    rRadius = radius * Mathf.Cos(ogiveT * Mathf.PI * 0.5f);
                }

                for (int i = 0; i < segments; i++)
                {
                    float angle = (i / (float)segments) * Mathf.PI * 2f;
                    float x = Mathf.Cos(angle) * rRadius;
                    float y = Mathf.Sin(angle) * rRadius;
                    vertices.Add(new Vector3(x, y, z));
                    normals.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), t * 0.5f).normalized);
                    uvs.Add(new Vector2(i / (float)segments, t));
                }
            }

            for (int r = 0; r < rings; r++)
            {
                int r1 = startRingIndex + r * segments;
                int r2 = startRingIndex + (r + 1) * segments;
                for (int i = 0; i < segments; i++)
                {
                    int next = (i + 1) % segments;
                    triangles.Add(r1 + i);
                    triangles.Add(r1 + next);
                    triangles.Add(r2 + next);

                    triangles.Add(r1 + i);
                    triangles.Add(r2 + next);
                    triangles.Add(r2 + i);
                }
            }

            // Ponta da bala (vértice final)
            int tipIndex = vertices.Count;
            vertices.Add(new Vector3(0f, 0f, length));
            normals.Add(Vector3.forward);
            uvs.Add(new Vector2(0.5f, 1f));

            int lastRing = startRingIndex + rings * segments;
            for (int i = 0; i < segments; i++)
            {
                triangles.Add(lastRing + i);
                triangles.Add(lastRing + (i + 1) % segments);
                triangles.Add(tipIndex);
            }

            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        private static GameObject CreateProjectilePrefab(Mesh bulletMesh, Material matBullet, Material matTrail, GameObject impactPrefab)
        {
            string path = $"{PREFAB_DIR}/Projectile_Player.prefab";
            var go = new GameObject("Projectile_Player");
            go.layer = LayerMask.NameToLayer("PlayerProjectile") >= 0 ? LayerMask.NameToLayer("PlayerProjectile") : 0;

            // Visual
            var mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = bulletMesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = matBullet;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // Colisor
            var col = go.AddComponent<CapsuleCollider>();
            col.isTrigger = true;
            col.radius = 0.08f;
            col.height = 0.30f;
            col.direction = 2; // Z-axis

            var rb = go.AddComponent<Rigidbody>();
            rb.isKinematic = true;

            // Componente Lógica
            var proj = go.AddComponent<Projectile>();

            // TrailRenderer de Fogo
            var tr = go.AddComponent<TrailRenderer>();
            tr.time = 0.15f;
            tr.minVertexDistance = 0.05f;
            tr.sharedMaterial = matTrail;

            var curve = new AnimationCurve();
            curve.AddKey(0f, 0.12f);
            curve.AddKey(1f, 0.0f);
            tr.widthCurve = curve;

            var grad = new Gradient();
            grad.SetKeys(
                new GradientColorKey[] {
                    new GradientColorKey(new Color(1f, 0.95f, 0.3f), 0f),
                    new GradientColorKey(new Color(1f, 0.45f, 0.05f), 0.35f),
                    new GradientColorKey(new Color(0.9f, 0.1f, 0.02f), 0.75f),
                    new GradientColorKey(new Color(0.2f, 0.0f, 0.0f), 1f)
                },
                new GradientAlphaKey[] {
                    new GradientAlphaKey(1f, 0f),
                    new GradientAlphaKey(0.85f, 0.5f),
                    new GradientAlphaKey(0f, 1f)
                }
            );
            tr.colorGradient = grad;

            // Luz pontual laranja fraca
            var lightGO = new GameObject("BulletLight");
            lightGO.transform.SetParent(go.transform, false);
            var light = lightGO.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.55f, 0.1f);
            light.range = 2.5f;
            light.intensity = 1.8f;
            light.shadows = LightShadows.None;

            // Partículas de fagulhas/fogo atrás da bala
            var psGO = new GameObject("Flames");
            psGO.transform.SetParent(go.transform, false);
            psGO.transform.localPosition = new Vector3(0, 0, -0.05f);
            var ps = psGO.AddComponent<ParticleSystem>();
            var psr = psGO.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = matTrail;

            var main = ps.main;
            main.startLifetime = 0.10f;
            main.startSpeed = 0.8f;
            main.startSize = 0.07f;
            main.startColor = new Color(1f, 0.6f, 0.1f, 0.9f);
            main.simulationSpace = ParticleSystemSimulationSpace.World;

            var emission = ps.emission;
            emission.rateOverTime = 30f;

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 15f;
            shape.radius = 0.02f;
            shape.rotation = new Vector3(0, 180f, 0); // apontando para trás

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            Debug.Log($"[OVERHAUL] Projectile_Player.prefab salvo em {path}");
            return prefab;
        }

        private static GameObject CreateMuzzleFlashPrefab(Material matTrail)
        {
            string path = $"{PREFAB_DIR}/MuzzleFlash.prefab";
            var go = new GameObject("MuzzleFlash");

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.65f, 0.15f);
            light.range = 3.2f;
            light.intensity = 4.5f;
            light.shadows = LightShadows.None;

            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = matTrail;

            var main = ps.main;
            main.duration = 0.05f;
            main.loop = false;
            main.startLifetime = 0.06f;
            main.startSpeed = 5.0f;
            main.startSize = 0.08f;
            main.startColor = new Color(1f, 0.8f, 0.2f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 8) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 25f;
            shape.radius = 0.03f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static GameObject CreateImpactPrefab(Material matTrail)
        {
            string path = $"{PREFAB_DIR}/ImpactEffect.prefab";
            var go = new GameObject("ImpactEffect");

            var light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.5f, 0.1f);
            light.range = 2.5f;
            light.intensity = 3.0f;

            var ps = go.AddComponent<ParticleSystem>();
            var psr = go.GetComponent<ParticleSystemRenderer>();
            psr.sharedMaterial = matTrail;

            var main = ps.main;
            main.duration = 0.1f;
            main.loop = false;
            main.startLifetime = 0.15f;
            main.startSpeed = 3.5f;
            main.startSize = 0.06f;
            main.startColor = new Color(1f, 0.5f, 0.1f, 1f);

            var emission = ps.emission;
            emission.rateOverTime = 0f;
            emission.SetBursts(new ParticleSystem.Burst[] { new ParticleSystem.Burst(0f, 12) });

            var shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.05f;

            var prefab = PrefabUtility.SaveAsPrefabAsset(go, path);
            UnityEngine.Object.DestroyImmediate(go);
            return prefab;
        }

        private static void UpdateWeaponDataSO(string assetName, GameObject projPrefab, GameObject muzzlePrefab, AudioClip shootSFX, AudioClip emptySFX, AudioClip reloadSFX)
        {
            string path = $"{SO_DIR}/{assetName}.asset";
            var so = AssetDatabase.LoadAssetAtPath<WeaponData>(path);
            if (so == null) return;

            var serObj = new SerializedObject(so);
            serObj.FindProperty("projectilePrefab").objectReferenceValue = projPrefab;
            serObj.FindProperty("muzzleFlashPrefab").objectReferenceValue = muzzlePrefab;
            serObj.FindProperty("shootSFX").objectReferenceValue = shootSFX;
            serObj.FindProperty("emptySFX").objectReferenceValue = emptySFX;
            serObj.FindProperty("reloadSFX").objectReferenceValue = reloadSFX;
            serObj.FindProperty("projectileSpeed").floatValue = 40.0f;
            serObj.FindProperty("projectileRange").floatValue = 45.0f;
            serObj.ApplyModifiedProperties();
            EditorUtility.SetDirty(so);
            Debug.Log($"[OVERHAUL] Atualizado {assetName}: Speed=40, ShootSFX={shootSFX.name}");
        }

        private static void UpdatePlayerInScene()
        {
            var scene = EditorSceneManager.OpenScene("Assets/_Project/Scenes/Arena.unity", OpenSceneMode.Single);
            var playerGO = GameObject.FindGameObjectWithTag("Player");
            if (playerGO == null) return;

            // 1. Hierarquia de Armas e WeaponSocket na mão direita
            var anim = playerGO.GetComponent<Animator>();
            Transform rightHand = null;
            if (anim != null && anim.isHuman)
            {
                rightHand = anim.GetBoneTransform(HumanBodyBones.RightHand);
            }

            Transform weaponSocket = playerGO.transform.Find("WeaponSocket");
            if (rightHand != null)
            {
                weaponSocket = rightHand.Find("WeaponSocket");
                if (weaponSocket == null)
                {
                    var wsGO = new GameObject("WeaponSocket");
                    weaponSocket = wsGO.transform;
                    weaponSocket.SetParent(rightHand, false);
                }
            }
            else if (weaponSocket == null)
            {
                var wsGO = new GameObject("WeaponSocket");
                weaponSocket = wsGO.transform;
                weaponSocket.SetParent(playerGO.transform, false);
            }

            // Ajuste anatômico de pegada na mão
            weaponSocket.localPosition = new Vector3(0.08f, 0.04f, 0.10f);
            weaponSocket.localRotation = Quaternion.Euler(0f, 90f, 0f);
            weaponSocket.localScale    = Vector3.one;

            // Mover WeaponHolder antigo para WeaponSocket se necessário
            var oldHolder = playerGO.transform.Find("WeaponHolder");
            if (oldHolder != null && oldHolder != weaponSocket)
            {
                while (oldHolder.childCount > 0)
                {
                    oldHolder.GetChild(0).SetParent(weaponSocket, false);
                }
                UnityEngine.Object.DestroyImmediate(oldHolder.gameObject);
            }

            // Garantir Muzzle na ponta do cano de cada arma
            foreach (Transform child in weaponSocket)
            {
                var shootPoint = child.Find("ShootPoint");
                if (shootPoint != null)
                {
                    shootPoint.localPosition = new Vector3(0f, 0.05f, 0.35f);
                    shootPoint.localRotation = Quaternion.identity;
                }
                var muzzle = child.Find("Muzzle");
                if (muzzle == null)
                {
                    var mGO = new GameObject("Muzzle");
                    muzzle = mGO.transform;
                    muzzle.SetParent(child, false);
                }
                muzzle.localPosition = new Vector3(0f, 0.05f, 0.35f);
                muzzle.localRotation = Quaternion.identity;

                // Atribuir no Weapon
                var weaponComp = child.GetComponent<Weapon>();
                if (weaponComp != null)
                {
                    var soW = new SerializedObject(weaponComp);
                    var mpProp = soW.FindProperty("muzzlePoint");
                    if (mpProp != null) mpProp.objectReferenceValue = muzzle;
                    soW.ApplyModifiedProperties();
                }
            }

            // Atribuir weaponHolder no PlayerShooter
            var shooter = playerGO.GetComponent<PlayerShooter>();
            if (shooter != null)
            {
                var sSO = new SerializedObject(shooter);
                var whProp = sSO.FindProperty("weaponHolder");
                if (whProp != null) whProp.objectReferenceValue = weaponSocket;
                sSO.ApplyModifiedProperties();
            }

            // 2. Animator simples: Idle e Walk
            var animCtrl = SetupCleanTwoStateAnimator();
            if (anim != null)
            {
                anim.runtimeAnimatorController = animCtrl;
                anim.applyRootMotion = false;
            }

            EditorSceneManager.MarkSceneDirty(scene);
            EditorSceneManager.SaveScene(scene);
            Debug.Log("[OVERHAUL] Cena Arena salva com WeaponSocket e Player atualizados.");
        }

        private static RuntimeAnimatorController SetupCleanTwoStateAnimator()
        {
            string path = "Assets/_Project/Animations/Player/AnimatorController_Player.controller";
            var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(path);
            if (controller == null)
            {
                controller = AnimatorController.CreateAnimatorControllerAtPath(path);
            }

            controller.parameters = new AnimatorControllerParameter[0];
            controller.AddParameter("Speed", AnimatorControllerParameterType.Float);
            controller.AddParameter("Shoot", AnimatorControllerParameterType.Trigger);
            controller.AddParameter("Die",   AnimatorControllerParameterType.Trigger);

            var sm = controller.layers[0].stateMachine;
            sm.states = new ChildAnimatorState[0];
            sm.anyStateTransitions = new AnimatorStateTransition[0];

            var gunClip = AssetDatabase.LoadAssetAtPath<AnimationClip>("Assets/_Project/ThirdParty/_INBOX/Mixamo/Gunplay.fbx");
            if (gunClip == null)
            {
                var sub = AssetDatabase.LoadAllAssetsAtPath("Assets/_Project/ThirdParty/_INBOX/Mixamo/Gunplay.fbx");
                foreach (var a in sub) if (a is AnimationClip c && !c.name.StartsWith("__preview__")) { gunClip = c; break; }
            }

            // Idle State
            var sIdle = sm.AddState("Idle");
            sIdle.motion = gunClip;
            sIdle.speed  = 1.0f;
            sm.defaultState = sIdle;

            // Walk State (usa Gunplay ajustado para locomoção)
            var sWalk = sm.AddState("Walk");
            sWalk.motion = gunClip;
            sWalk.speed  = 1.25f; // velocidade proporcional

            // Transição Idle -> Walk com amortecimento
            var tToWalk = sIdle.AddTransition(sWalk);
            tToWalk.AddCondition(AnimatorConditionMode.Greater, 0.1f, "Speed");
            tToWalk.duration = 0.20f;
            tToWalk.hasExitTime = false;

            // Transição Walk -> Idle
            var tToIdle = sWalk.AddTransition(sIdle);
            tToIdle.AddCondition(AnimatorConditionMode.Less, 0.05f, "Speed");
            tToIdle.duration = 0.20f;
            tToIdle.hasExitTime = false;

            EditorUtility.SetDirty(controller);
            return controller;
        }

        // ==================== GERADORES DE ÁUDIO WAV ====================
        private static void GenerateShootWav(string filePath)
        {
            int sampleRate = 44100;
            float duration = 0.18f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;

                // Frequência descendo rapidamente (800Hz -> 120Hz)
                float freq = Mathf.Lerp(800f, 120f, Mathf.Pow(progress, 0.4f));
                float wave = Mathf.Sin(2f * Mathf.PI * freq * t);

                // Ruído filtrado
                float noise = (UnityEngine.Random.value * 2f - 1f) * Mathf.Exp(-progress * 20f);

                // Envelope de decaimento rápido
                float envelope = Mathf.Exp(-progress * 12f);
                samples[i] = Mathf.Clamp((wave * 0.7f + noise * 0.3f) * envelope, -1f, 1f);
            }
            WriteWavFile(filePath, samples, sampleRate);
        }

        private static void GenerateEmptyWav(string filePath)
        {
            int sampleRate = 44100;
            float duration = 0.06f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = i / (float)sampleRate;
                float progress = t / duration;
                float wave = Mathf.Sin(2f * Mathf.PI * 1400f * t);
                float envelope = Mathf.Exp(-progress * 40f);
                samples[i] = wave * envelope * 0.6f;
            }
            WriteWavFile(filePath, samples, sampleRate);
        }

        private static void GenerateReloadWav(string filePath)
        {
            int sampleRate = 44100;
            float duration = 0.35f;
            int numSamples = (int)(sampleRate * duration);
            float[] samples = new float[numSamples];

            for (int i = 0; i < numSamples; i++)
            {
                float t = i / (float)sampleRate;
                // Clique 1 em 0.05s e Clique 2 em 0.22s
                float env1 = Mathf.Exp(-Mathf.Abs(t - 0.05f) * 60f);
                float env2 = Mathf.Exp(-Mathf.Abs(t - 0.22f) * 60f);
                float wave1 = Mathf.Sin(2f * Mathf.PI * 950f * t) * env1;
                float wave2 = Mathf.Sin(2f * Mathf.PI * 1250f * t) * env2;
                samples[i] = Mathf.Clamp((wave1 + wave2) * 0.7f, -1f, 1f);
            }
            WriteWavFile(filePath, samples, sampleRate);
        }

        private static void WriteWavFile(string filePath, float[] samples, int sampleRate)
        {
            using (var fs = new FileStream(filePath, FileMode.Create))
            using (var bw = new BinaryWriter(fs))
            {
                short numChannels = 1;
                short bitsPerSample = 16;
                int byteRate = sampleRate * numChannels * (bitsPerSample / 8);
                short blockAlign = (short)(numChannels * (bitsPerSample / 8));
                int subChunk2Size = samples.Length * numChannels * (bitsPerSample / 8);
                int chunkSize = 36 + subChunk2Size;

                // RIFF header
                bw.Write(System.Text.Encoding.ASCII.GetBytes("RIFF"));
                bw.Write(chunkSize);
                bw.Write(System.Text.Encoding.ASCII.GetBytes("WAVE"));

                // fmt chunk
                bw.Write(System.Text.Encoding.ASCII.GetBytes("fmt "));
                bw.Write(16);
                bw.Write((short)1); // PCM
                bw.Write(numChannels);
                bw.Write(sampleRate);
                bw.Write(byteRate);
                bw.Write(blockAlign);
                bw.Write(bitsPerSample);

                // data chunk
                bw.Write(System.Text.Encoding.ASCII.GetBytes("data"));
                bw.Write(subChunk2Size);

                for (int i = 0; i < samples.Length; i++)
                {
                    short val = (short)(Mathf.Clamp(samples[i], -1f, 1f) * 32767);
                    bw.Write(val);
                }
            }
        }
    }
}

