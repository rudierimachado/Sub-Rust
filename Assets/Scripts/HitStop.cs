using System.Collections;
using UnityEngine;

/// <summary>
/// Congelamento curto no impacto, com contagem de aninhamento.
///
/// Por que nao e' so' "guarda o timeScale e restaura": dois golpes proximos rodavam
/// duas rotinas ao mesmo tempo, e a segunda guardava 0,05 como "valor original" -
/// ao terminar, devolvia o jogo para 0,05 e ele ficava em camera lenta pra sempre.
/// Aqui so' o primeiro a entrar guarda a escala real, e so' o ultimo a sair restaura.
/// </summary>
public static class HitStop
{
    private static int ativos;
    private static float escalaOriginal = 1f;

    public static void Aplicar(MonoBehaviour dono, float segundos, float escala = 0.05f)
    {
        if (dono == null || segundos <= 0f) return;
        dono.StartCoroutine(Rotina(segundos, escala));
    }

    private static IEnumerator Rotina(float segundos, float escala)
    {
        if (ativos == 0) escalaOriginal = Time.timeScale;
        ativos++;
        Time.timeScale = escala;

        yield return new WaitForSecondsRealtime(segundos);

        ativos--;
        if (ativos <= 0)
        {
            ativos = 0;
            Time.timeScale = escalaOriginal;
        }
    }
}
