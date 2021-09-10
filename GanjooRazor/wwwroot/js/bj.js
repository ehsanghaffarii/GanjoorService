﻿// From David Flanagan's "JavaScript: The Definitive Guide" 5th Ed,
//   http://www.davidflanagan.com/javascript5/display.php?n=15-4&f=15/04.js
//modified 4 ganjoor a little bit

function isMember(element, classname) {
    var classes = element.className;
    if (!classes) return false;
    if (classes == classname) return true;
    var whitespace = /\s+/;
    if (!whitespace.test(classes)) return false;
    if (typeof classes != 'string') return false;
    var c = classes.split(whitespace);
    for (var i = 0; i < c.length; i++) {
        if (c[i] == classname) return true;
    }
    return false;
}
function getElements(classname, classname2, classname3, classname4, tagname, root) {
    if (!root) root = document;
    else if (typeof root == "string") root = document.getElementById(root);
    if (!tagname) tagname = "*";
    var all = root.getElementsByTagName(tagname);
    if (!classname) return all;
    var elements = [];
    for (var i = 0; i < all.length; i++) {
        var element = all[i];
        if (isMember(element, classname) || isMember(element, classname2) || isMember(element, classname3) || isMember(element, classname4))
            elements.push(element);
    }
    return elements;

}

function bnumClick(poemId, index) {
    var msr1s = getElements("m1", "b2", "n", "l");
    if (msr1s.length <= index) return;
    var divId = 'bnumpanel' + String(index);
    var existingDiv = document.getElementById(divId);
    if (existingDiv != null) {
        existingDiv.remove();
        return;
    }
    var divParent = msr1s[index].className == "m1" ? msr1s[index].parentElement : msr1s[index];
    divParent.innerHTML = divParent.innerHTML + '<div class="bnumdiv" id="' + divId + '"></div>';
    $.ajax({
        type: "GET",
        url: '?Handler=BNumPartial&poemId=' + String(poemId) + '&coupletIndex=' + String(index),
        success: function (data) {
            var divId = 'bnumpanel' + String(index);
            $(data).appendTo(document.getElementById(divId));
        },
    });
    
}

function coupletNumImage(bnum, color) {
    let canvas = document.createElement('canvas');
    canvas.width = 50;
    canvas.height = 28;
    let context = canvas.getContext('2d');
    context.font = "1.5em Vazir";
    context.fillStyle = color;
    context.textAlign = "center";
    context.textBaseline = "top";
    context.imageSmoothingEnabled = true;
    context.fillText(bnum, canvas.width / 2, 7);
    return canvas.toDataURL();
}

function bshfarsinum(englishnum) {
    var result = "";
    for (var i = 0; i < englishnum.length; i++) {
        result = result + String.fromCharCode(englishnum.charCodeAt(i) + 0x6C0);
    }
    return result;
}



function btshmr_internal(poemId) {
    var bnum = getElements("bnum");
    if (bnum.length != 0) {
        for (var i = 0; i < bnum.length; ++i) {
            bnum[i].remove();
        }
        var bnumdiv = getElements("bnumdiv");
        for (var i = 0; i < bnumdiv.length; ++i) {
            bnumdiv[i].remove();
        }
        document.getElementById("bnum-button").innerText = "شماره‌گذاری ابیات";
        return true;
    }
    document.getElementById("bnum-button").innerText = "حذف شماره‌ها";
    var msr1s = getElements("m1", "b2", "n", "l");
    if (msr1s.length == 0) return true;
    var j = 0;
    var k = 1;
    for (var i = 0; i < msr1s.length; ++i) {
        if (msr1s[i].className != "b2") {
            if (msr1s[i].className != "m1") {
                msr1s[i].innerHTML = '<div class="bnum normalbnum" onclick="bnumClick(' + String(poemId) + ',' + String(i) + ');" id="bnum' + String(i + 1) + '"></div>' + msr1s[i].innerHTML;//no alt, so that when user copies text does not appear in the copied content
            }
            else {
                msr1s[i].parentElement.innerHTML = '<div class="bnum normalbnum" onclick="bnumClick(' + String(poemId) + ',' + String(i) + ');" id="bnum' + String(i + 1) + '"></div>' + msr1s[i].parentElement.innerHTML;//no alt, so that when user copies text does not appear in the copied content
            }
            
            document.getElementById("bnum" + String(i + 1)).style.background = 'url(' + coupletNumImage(bshfarsinum(String(j + 1)), "red") + ')';
            j++;
        }
        else {
            msr1s[i].innerHTML = '<div class="bnum bandnum" onclick="bnumClick(' + String(poemId) + ',' + String(i) + ');" id="bnum' + String(i + 1) + '"></div>' + msr1s[i].innerHTML;
            document.getElementById("bnum" + String(i + 1)).style.background = 'url(' + coupletNumImage("بند " + bshfarsinum(String(k)), "blue") + ')';
            k++;
            j = 0;
        }
    }
    return true;
}

function btshmr(poemId) {
    setTimeout(function () { btshmr_internal(poemId); }, 1);
}



function hilightverse(vnum, clr, sc, forceScroll) {
    var root = document;
    if (typeof root == "string") root = document.getElementById(root);
    var all = root.getElementsByTagName("*");
    var n = -1;
    for (var i = 0; i < all.length; i++) {
        var element = all[i];
        if (isMember(element, "m1") || isMember(element, "m2") || isMember(element, "n") || isMember(element, "l")) {
            n++;
            if (n == vnum) {
                element.style.color = clr;
                if ($('#InlinePauseButton') != null) {
                    $('#InlinePauseButton').remove();
                }
                if (sc == true) {
                    var btn = document.createElement("BUTTON");
                    btn.id = "InlinePauseButton";
                    var t = document.createTextNode(" || ");
                    btn.appendChild(t);
                    btn.style["display"] = "inline";
                    btn.style["width"] = "25px";
                    btn.onclick = function () {
                        if ($('#jquery_jplayer_1').data().jPlayer.status.paused) {
                            $('#jquery_jplayer_1').data().jPlayer.play();
                        }
                        else {
                            $('#jquery_jplayer_1').data().jPlayer.pause();
                        }
                    };
                    element.lastChild.appendChild(btn);

                    if (forceScroll)
                        if (!!element && element.scrollIntoView) {
                            element.scrollIntoView();
                        }
                }
                return true;
            }
        }
        else
            if (isMember(element, "b2")) {
                var ptags = element.getElementsByTagName("p");
                for (var p = 0; p < ptags.length; p++) {
                    if (ptags[p].parentElement.className == "bnum")
                        continue;
                    n++;
                    if (n == vnum) {
                        ptags[p].style.color = clr;
                        if (ptags[p].getElementsByTagName("BUTTON").length == 1) {
                            ptags[p].removeChild(ptags[p].getElementsByTagName("BUTTON")[0]);
                        }

                        if (sc == true) {
                            var btn = document.createElement("BUTTON");
                            var t = document.createTextNode(" || ");
                            btn.appendChild(t);
                            btn.style["display"] = "inline";
                            btn.style["width"] = "25px";
                            btn.onclick = function () {
                                if ($('#jquery_jplayer_1').data().jPlayer.status.paused) {
                                    $('#jquery_jplayer_1').data().jPlayer.play();
                                }
                                else {
                                    $('#jquery_jplayer_1').data().jPlayer.pause();
                                }
                            };
                            ptags[p].appendChild(btn);

                            if (forceScroll)
                                if (!!element && element.scrollIntoView) {
                                    element.scrollIntoView();
                                }
                        }
                        return true;
                    }
                }

            }
    }
    return false;
}

var audioxmlfiles = [];
var narrators = [];
var jlist;
function addpaudio(index, jplaylist, xmlfilename, poemtitle, auartist, oggurl, mp3url) {
    audioxmlfiles[index] = xmlfilename;
    narrators[index] = auartist;
    jplaylist.add({
        title: poemtitle,
        artist: auartist,
        mp3: mp3url
    });
}
function prepaudio(xmlfilename, poemtitle, auartist, oggurl, mp3url) {
    var verseStart = [];
    var verseEnd = [];
    var verseIndex = [];
    var nLastHighlight = -1;
    var vCount = 0;

    audioxmlfiles[0] = xmlfilename;
    narrators[0] = auartist;


    jlist = new jPlayerPlaylist({
        jPlayer: "#jquery_jplayer_1",
        cssSelectorAncestor: "#jp_container_1"
    }, [
        {
            title: poemtitle,
            artist: auartist,
            mp3: mp3url
        },
    ],
        {
            setmedia: function (event) {

                $.ajax({
                    type: "GET",
                    url: audioxmlfiles[jlist.current],
                    dataType: "xml",
                    success: function (xml) {
                        var nOneSecondBugFix = 2000;
                        $(xml).find('OneSecondBugFix').each(function () {
                            nOneSecondBugFix = parseInt($(xml).find('OneSecondBugFix').text());
                        });
                        var v = 0;
                        $(xml).find('SyncInfo').each(function () {
                            verseStart[v] = parseInt($(this).find('AudioMiliseconds').text()) / nOneSecondBugFix;
                            verseIndex[v] = parseInt($(this).find('VerseOrder').text());
                            if (v > 0)
                                verseEnd[v - 1] = verseStart[v];
                            v++;
                        });
                        v--;
                        if (v > 1)
                            verseEnd[v] = verseStart[v] + 2 * (verseEnd[v - 1] - verseStart[v - 1]);
                        vCount = v;
                    }
                });


            },

            timeupdate: function (event) { // 4Hz
                var curTime = event.jPlayer.status.currentTime;
                if (curTime > 0) {
                    for (i = 0; i <= vCount; i++) {
                        if (curTime >= verseStart[i] && curTime <= verseEnd[i]) {
                            hilightverse(verseIndex[i], "red", true, playerScrollLock);

                            if (nLastHighlight != verseIndex[i] && nLastHighlight != -1)
                                hilightverse(nLastHighlight, "black", false, false);
                            nLastHighlight = verseIndex[i];
                            break;
                        }
                    }
                }

            },
            ended: function (event) { // 4Hz
                if (nLastHighlight != -1)
                    hilightverse(nLastHighlight, "black", false, false);
            },
            swfPath: "dist/jplayer",
            supplied: "oga, mp3",
            wmode: "window",
            useStateClassSkin: true,
            autoBlur: false,
            smoothPlayBar: true,
            keyEnabled: true,
            remainingDuration: true,
            toggleDuration: true
        });

    return jlist;

}

function fillnarrations(coupletIndex) {
    if (narrators.length == 0) {
        var blockid = '#play-block-' + coupletIndex;
        $(blockid).hide();
        return;
    }

    var comboId = '#narrators-' + coupletIndex;
    for (var i = 0; i < narrators.length; i++) {
        $(comboId).append(new Option(narrators[i].replace(/<\/?[^>]+(>|$)/g, "").replace('به خوانش ', '').replace('می‌خواهید شما بخوانید؟ اینجا را ببینید.', '').replace('(دریافت)', ''), i));
    }
}

function getVerseIndexFromCoupleIndex(coupletIndex) {
    var tagname = "*";
    var all = document.getElementsByTagName(tagname);
    var msr1s = [];
    for (var i = 0; i < all.length; i++) {
        var element = all[i];
        if (isMember(element, "m1") || isMember(element, "m2") || isMember(element, "b2") || isMember(element, "n") || isMember(element, "l"))
            msr1s.push(element);
    }
    if ((msr1s.length - 1) < coupletIndex) return -1;
    var cIndex = -1;
    var vIndex = -1;
    var bandNums = 0;
    for (var i = 0; i < msr1s.length; ++i) {
        if (msr1s[i].className != "m2") {
            cIndex++;
        }
        
        if (cIndex == coupletIndex) {
            vIndex = (i + bandNums);
            break;
        }

        if (msr1s[i].className == "b2") {
            if (msr1s[i].getElementsByTagName("p").length > 1)
                bandNums++;
        }
        
    }
    return vIndex;
}


function playCouplet(coupletIndex) {
    
    var vIndex = getVerseIndexFromCoupleIndex(coupletIndex);
    if (jlist.isPlaying) {
        jlist.pause();
    }
    var comboId = '#narrators-' + coupletIndex;
    var recitationIndex =  $(comboId).find(":selected").val()
    jlist.select(recitationIndex);

    if (audioxmlfiles.length > 0) {
        $.ajax({
            type: "GET",
            url: audioxmlfiles[recitationIndex],
            dataType: "xml",
            success: function (xml) {
                var nOneSecondBugFix = 2000;
                $(xml).find('OneSecondBugFix').each(function () {
                    nOneSecondBugFix = parseInt($(xml).find('OneSecondBugFix').text());
                });

                var foundCouplet = false;
                
                $(xml).find('SyncInfo').each(function () {
                    var v = parseInt($(this).find('VerseOrder').text())
                    if (v == vIndex) {
                        var verseStart = parseInt($(this).find('AudioMiliseconds').text()) / nOneSecondBugFix;
                        $(jlist.cssSelector.jPlayer).jPlayer("play");
                        setTimeout(function () {
                            $(jlist.cssSelector.jPlayer).jPlayer("play", verseStart);
                        }, 100);
                        foundCouplet = true;
                        var buttonList = '#listen-' + coupletIndex;
                        $(buttonList).text('در حال خواندن');
                        return false;
                    }
                });

                if (!foundCouplet) {
                    alert('در این خوانش این خط خوانده نشده است.');
                }
            }
        });
    }
}

function editCouplet(poemId, coupletIndex) {

    var vIndex = getVerseIndexFromCoupleIndex(coupletIndex);
    location.href = '/User/Editor?id=' + poemId + '#id-' + String(vIndex + 1);
}