using System;
using UnityEngine;

namespace CampusRun.Character
{
    /// <summary>
    /// 캠퍼스런에서 선택 가능한 플레이어 캐릭터 데이터 클래스입니다.
    /// </summary>
    [Serializable]
    public class CharacterInfo
    {
        [Tooltip("고유 ID")]
        public string id;

        [Tooltip("캐릭터 이름 (예: 새내기 민우, 뒤뚱 펭귄)")]
        public string displayName;

        [Tooltip("3D 캐릭터 모델 FBX 또는 프리팹")]
        public GameObject modelPrefab;

        [Tooltip("UI 표시용 2D 썸네일 텍스처")]
        public Texture2D previewTexture;

        [Tooltip("공용 머티리얼 덮어쓰기 여부 (Kenney 캐릭터는 true, 펭귄 등 자체 텍스처 모델은 false)")]
        public bool overrideMaterial = true;

        [Tooltip("모델 고유 스케일 배율 (기본 1.0)")]
        public float customScale = 1.0f;

        public CharacterInfo(string id, string displayName, GameObject modelPrefab, Texture2D previewTexture, bool overrideMaterial = true, float customScale = 1.0f)
        {
            this.id = id;
            this.displayName = displayName;
            this.modelPrefab = modelPrefab;
            this.previewTexture = previewTexture;
            this.overrideMaterial = overrideMaterial;
            this.customScale = customScale;
        }
    }
}
