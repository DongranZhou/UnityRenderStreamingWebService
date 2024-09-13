using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Answer
{

    public string sdp {  get; set; }
    public long datetime { get; set; }
    public Answer() { }
    public Answer(string sdp,long datetime) 
    {
        this.sdp = sdp;
        this.datetime = datetime;
    }
}