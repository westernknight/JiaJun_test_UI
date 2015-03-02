using UnityEngine;
using System.Collections;

public class MyLoader 
{

	 WWW m_www = null;
 
    public IEnumerator StartTimeout(float timeout)
    {
        yield return new WaitForSeconds(timeout);
        ClearWWW();
    }
 
    public IEnumerator Load(string url)
    {
        m_www = new WWW(url);
#if UNITY_IPHONE
        while (m_www != null  !m_www.isDone) { yield return null; }
#else
        yield return m_www; // Cannot be interrupted by m_www.Dispose() on iOS
#endif
       // ProcessResponse(m_www);
        ClearWWW();
    }
 
    public void ClearWWW()
    {
        if (m_www != null) {
            m_www.Dispose();
            m_www = null;
            System.GC.Collect(); // Optional --- just runs the garbage collector
        }
    }
 
}
