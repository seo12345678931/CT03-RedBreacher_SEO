using UnityEngine;

/// <summary>
/// 앱 시작 시 목표 프레임레이트를 60으로 고정한다.
/// 씬·게임오브젝트에 붙일 필요 없이 첫 씬 로드 전에 자동 실행되므로 전 씬에 일괄 적용된다.
/// vSync가 켜져 있으면 targetFrameRate가 무시되기 때문에 함께 꺼 준다.
/// </summary>
public static class FrameRateBootstrap
{
    private const int TargetFrameRate = 60;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Apply()
    {
        QualitySettings.vSyncCount = 0;            // vSync가 켜져 있으면 targetFrameRate가 적용되지 않음.
        Application.targetFrameRate = TargetFrameRate;
    }
}
