using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Video;


public class video_player_play : MonoBehaviour {
	VideoPlayer v;
	

	// Use this for initialization
	void Start () {
		v = GameObject.Find("Video Player").GetComponent<VideoPlayer>();
	}
	
	// Update is called once per frame
	void Update () {
		
	}

	void play() {
		v.Play();
	}
}
