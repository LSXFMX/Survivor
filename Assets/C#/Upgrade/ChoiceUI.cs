using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ChoiceUI : MonoBehaviour
{
    public List<GameObject> list;
    public List<GameObject> upplayer;
    public List<GameObject> upfireball;
    public List<GameObject> uphurricane;
    public List<GameObject> updarkbear;
    public List<GameObject> newskill;

    public Transform choice1;
    public Transform choice2;
    public Transform choice3;
    public Transform playerskill;
    void OnEnable()
    {
        list = new List<GameObject>();
    }
    public void refresh()//刷新三选一选项框
    {
        list = new List<GameObject>();
        list.AddRange(upplayer);
        list.AddRange(upfireball);
        bool havehurricane =false;
        bool havedarkbear=false;
        foreach (Transform skill in playerskill)
        {
            Skillbase s = skill.GetComponent<Skillbase>();
            if(s.Skillname=="hurricane")
            {
                havehurricane = true;
            }
            if (s.Skillname == "darkbear")
            {
                havedarkbear = true;
            }
        }
        if(havehurricane)
        {
            list.AddRange(uphurricane);

        }
        else
        {
            GameObject hurricaneSkill = newskill[1];
            list.Add(hurricaneSkill);
        }
        if (havedarkbear)
        {
            list.AddRange(updarkbear);
            
        }
        else
        {
            GameObject darkbearSkill = newskill[0];
            list.Add(darkbearSkill);
        }




        refreshsignalchoice(choice1);
        refreshsignalchoice(choice2);
        refreshsignalchoice(choice3);
    }
    public void refreshsignalchoice(Transform choice)
    {
        
    }
}
