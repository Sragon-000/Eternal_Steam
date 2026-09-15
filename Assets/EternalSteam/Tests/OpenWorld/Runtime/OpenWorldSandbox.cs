using System;
using System.Collections.Generic;
using EternalSteam.Demo;
using UnityEngine;
namespace EternalSteam.OpenWorld
{
    public sealed class OpenWorldSandbox : MonoBehaviour
    {
        public bool SceneAuthored;
        public GameObject FoundationPrefab;
        public GameObject[] TowerPrefabs;
        public Terrain Ground;
        public FreeCameraRig CameraRig;
        public Mesh EnemyMesh;
        public Material EnemyMaterial, TowerMaterial, BarrelMaterial, LineMaterial, ValidMaterial, InvalidMaterial, FoundationMaterial;
        public FoundationPlacement Foundations { get; private set; }
        public HordeEnemyWorld Enemies { get; private set; }
        public readonly List<HordeTower> Towers=new();
        public bool Running;
        public string Message="토대 설치를 눌러 평탄한 지형에 토대를 놓으세요.";
        HordeTowerCombat combat;
        HordeEnemyRenderer renderer;
        readonly Material[] types=new Material[4],effects=new Material[4];
        System.Random random=new(731);
        void Awake()
        {
            Enemies=new HordeEnemyWorld(p=>Ground.SampleHeight(p)+Ground.transform.position.y+.55f,false);
            for(int i=0;i<4;i++) {
                types[i]=new Material(TowerMaterial);types[i].SetColor("_BaseColor",HordeTowerStats.Color((HordeTowerKind)i));
                effects[i]=new Material(LineMaterial);effects[i].SetColor("_BaseColor",HordeTowerStats.Color((HordeTowerKind)i));
            }
            Foundations=new FoundationPlacement(Ground,transform,Towers,FoundationMaterial,BarrelMaterial,LineMaterial,ValidMaterial,types,effects,FoundationPrefab,TowerPrefabs);
            if(SceneAuthored) {
                foreach(var platform in GetComponentsInChildren<SceneFoundation>())Foundations.Adopt(platform);
                foreach(var tower in GetComponentsInChildren<SceneTower>())Foundations.Adopt(tower);
                Message="토대 설치 버튼 → 지형 클릭 → 포탑 선택 → 토대 칸과 공격 방향 클릭";
            }
            combat=new HordeTowerCombat(Towers,Enemies,new HordeAttackResolver(Enemies),true);
            var size=Ground.terrainData.size;
            renderer=new HordeEnemyRenderer(Enemies,EnemyMesh,EnemyMaterial,new Bounds(Ground.transform.position+size*.5f,size+Vector3.up*20));
        }
        public bool Spawn(string amount)
        {
            if(!int.TryParse(amount,out int count) || count<1 || count>HordeEnemyWorld.Capacity) {Message="소환 수량은 1~4,000 사이의 정수로 입력하세요.";return false;}
            if(count>Enemies.FreeCount){Message=$"남은 소환 공간은 {Enemies.FreeCount:N0}마리입니다. 수량을 줄이거나 적 초기화를 누르세요.";return false;}
            var center=CameraRig.Focus;var origin=Ground.transform.position;var size=Ground.terrainData.size;
            center.x=Mathf.Clamp(center.x,origin.x+36,origin.x+size.x-36);center.z=Mathf.Clamp(center.z,origin.z+12,origin.z+size.z-12);
            for(int i=0;i<count;i++) {
                float z=(float)random.NextDouble()*14-7;
                var start=center+new Vector3(-30-(float)random.NextDouble()*3,0,z);
                var end=center+new Vector3(30,0,z);
                Enemies.TrySpawn(start,end,2+(float)random.NextDouble()*.8f);
            }
            Running=true;Message=$"{count:N0}마리 소환 — 화면 중심의 왼쪽에서 오른쪽으로 이동합니다.";return true;
        }
        public void ResetEnemies(){Enemies.Reset();combat.Reset();random=new System.Random(731);Running=false;Message="적을 초기화했습니다. 토대와 포탑은 유지됩니다.";}
        void Update()
        {
            if(Running){float dt=Mathf.Min(Time.deltaTime,.1f);Enemies.MoveAndIndex(dt);combat.Update(dt,true,int.MaxValue);}
            else foreach(var t in Towers) HordeTowerEffects.Tick(t,Time.deltaTime,false,false);
            renderer.Draw();
        }
        void OnDestroy()
        {
            Foundations?.Dispose();renderer?.Dispose();
            foreach(var m in types)if(m!=null)Destroy(m);foreach(var m in effects)if(m!=null)Destroy(m);
        }
    }
}
