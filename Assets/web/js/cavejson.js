/*
    caveJSON.js - wrapper for CaveJSON data
*/
/*jshint esversion: 6 */
/*global window */
/*global $ */
/*global Handlebars */

$(function () {
	$('[data-toggle="tooltip"]').tooltip();
});

String.prototype.toHHMMSS = function () {
	let sec_num = parseInt(this, 10); // don't forget the second param
	return sec_num.toHHMMSS();
};

Number.prototype.toHHMMSS = function () {
	let hours = Math.floor(this / 3600);
	let minutes = Math.floor((this - (hours * 3600)) / 60);
	let seconds = this - (hours * 3600) - (minutes * 60);

	if (hours < 10) {
		hours = "0" + hours;
	}
	if (minutes < 10) {
		minutes = "0" + minutes;
	}
	if (seconds < 10) {
		seconds = "0" + seconds;
	}
	return hours + ':' + minutes + ':' + seconds;
};

String.prototype.toMMSS = function () {
	let sec_num = parseInt(this, 10); // don't forget the second param
	return sec_num.toMMSS();
};


Number.prototype.toMMSS = function () {
	let minutes = Math.floor(this / 60);
	let seconds = this - (minutes * 60);

	if (minutes < 10) {
		minutes = "0" + minutes;
	}
	if (seconds < 10) {
		seconds = "0" + seconds;
	}
	return minutes + ':' + seconds;
};


Number.prototype.getSecondsFromMSeconds = function () {
	return Math.floor(this / 1000);
};

if (window.CaveJSON !== undefined) {

	window.CaveJSON = (function () {

		const splitItems = function (str) {
            if (!str) return "";
			let items = str.split(';');
			let x = [];
			for (let n = 0; n < items.length; n++) {
				if (items[n].trim().length === 0) continue;
				x.push(items[n]);
			}
			return x;
		};

		const getTimeStringFromMS = function (msecs) {
			let sec = Math.floor(msecs / 1000);
			return sec.toMMSS();
		};

		const getTimeStringFromMSLong = function (msecs) {
			let sec = Math.floor(msecs / 1000);
			return sec.toHHMMSS();
		};

		let _CJ = {};
		_CJ.data = {};
		_CJ.lookUps = {};
		_CJ.clientHook = null;

		_CJ.getURLParams = function (queryString) {
			queryString = queryString.split('+').join(' ');

			let params = {},
				re = /[?&]?([^=]+)=([^&]*)/g,
				tokens = re.exec(queryString);

			while (tokens) {
				params[decodeURIComponent(tokens[1])] = decodeURIComponent(tokens[2]);
				tokens = re.exec(queryString);
			}

			return params;
		};

		_CJ.parseNewData = function (newJCData) {

			//         window.console.log('new Data:', newJCData);

			//      Object.assign(_CJ.data, newJCData);
			_CJ.data = newJCData;

			_CJ.errorMsg = "";
			_CJ.resultOK = _CJ.data.Messages !== undefined;

			for (let i = 0; i < _CJ.data.Messages.RowCount; i++) {
				let ok = (_CJ.data.Messages.Rows[i].Code === 'OK');
				_CJ.resultOK &= ok;
				if (!ok) {
					_CJ.errorMsg += _CJ.data.Messages.Rows[i].Content + "\r\n";
				}
			}

			// do only if result OK
			if (!_CJ.resultOK) {
				return;
			}


			// todo remove css styles
			if (_CJ.data.UserSessions && _CJ.data.UserSessions.Rows.length === 1) {
				_CJ.Session = Object.assign({}, _CJ.data.UserSessions.Rows[0]);
				let path = window.location.pathname.split('.')[0];
				// window.console.log(path);
				_CJ.Session.Path = path;
				if ((path === '/') || (path === '/index')) {
					_CJ.Session.IndexClass = "text-danger";
				} else {
					_CJ.isNotMainPage = true;
				}
                if (path === '/admin/settings') _CJ.Session.SettingsClass = "text-danger";
				if (path === '/artists') _CJ.Session.ArtistsClass = "text-danger";
				if (path === '/albums') _CJ.Session.AlbumsClass = "text-danger";
				if (path === '/audiofiles') _CJ.Session.TitlesClass = "text-danger";
				if (path === '/login') _CJ.Session.LoginClass = "text-danger";


				// TODO remove this section and build other settings breadcrumb
				const adminPages = ['Settings', 'Globals', 'Subsets', 'Crawler', 'Users', 'System-Log'];
				_CJ.Session.AdminPages = [];
				for (let i = 0; i < adminPages.length; i++) {
					let p = {
						Name: adminPages[i],
						Link: '/admin/' + adminPages[i].toLowerCase(),
					};
					if (p.Link === path) {
                        p.ActiveClass = "active bg-danger";
                        if (i > 0) _CJ.Session.isAdminSubPage = adminPages[i];
                    }
					_CJ.Session.AdminPages.push(p);
				}

				// window.console.log(_CJ.Session.Flags);
				_CJ.Session.isLocalHost = (_CJ.Session.Flags.indexOf('IsLocalhost') > -1);

				// window.console.log('Sess Ava: ' + JSON.stringify(_CJ.data.Users.Rows[0].AvatarID));
			}


			const rKeys = ['Layout', 'Version', 'Result', 'Messages', 'UserSessions', 'TemplateParameters', 'Pagination'];
			for (let name in _CJ.data) {
				if (_CJ.data.hasOwnProperty(name) && (rKeys.indexOf(name) === -1)) {
					switch (_CJ.data[name].Type) {
						case 'Table':
							if (Array.isArray(_CJ.data[name].Rows)) {
								_CJ[name] = _CJ.data[name].Rows.slice();
							} else {
								_CJ[name] = Object.assign({}, _CJ.data[name].Rows);
							}
							_CJ.lookUps[name] = _CJ.data[name].Lookup;
							break;
					}
				}
			}


			_CJ.Params = _CJ.getURLParams(window.location.search);
			_CJ.ParamCount = Object.getOwnPropertyNames(_CJ.Params).length;
			let SubParams = {};
			for (let p in _CJ.Params) {
				if (_CJ.Params.hasOwnProperty(p) && (['a', 'b', 'c', 'g', 't'].indexOf(p) > -1)) {
					SubParams[p] = _CJ.Params[p];
				}
			}
			_CJ.SubParams = $.param(SubParams);

			if (_CJ.data.TemplateParameters && (_CJ.data.TemplateParameters.Rows.length > 0)) {
				let tp = {};
				for (let i = 0; i < _CJ.data.TemplateParameters.Rows.length; i++) {
					let r = _CJ.data.TemplateParameters.Rows[i];
					if (tp[r.FunctionName] === undefined) tp[r.FunctionName] = {};
					tp[r.FunctionName][r.ParameterAtTemplate] = r.ParameterAtFunction;
				}
				_CJ.TemplateParameters = tp;
			}

			_CJ.mapTemplateParameters = function (name, params) {
				// window.console.log(params);
				let result = {};
				if (_CJ.TemplateParameters.hasOwnProperty(name)) {
				    let tparams = _CJ.TemplateParameters[name];
					for (let key in params) {
						if (params.hasOwnProperty(key) && tparams.hasOwnProperty(key)) {
							result[tparams[key]] = params[key];
						}
					}
				}
				return result;
			};

			if (_CJ.data.Pagination && _CJ.data.Pagination.Rows.length === 1) {
				_CJ.Pagination = _CJ.data.Pagination.Rows[0];
			}


			if (_CJ.Users && _CJ.Users.length > 0) {
				let lookup = -1;
				window.editUserName = {};
				for (let i = 0; i < _CJ.Users.length; i++) {
					if (_CJ.lookUps.UserLevels) {
						lookup = _CJ.lookUps.UserLevels[_CJ.Users[i].AuthLevel];
						if (lookup > -1) {
							_CJ.Users[i].AuthLevelName = _CJ.UserLevels[lookup].Name;
						} else {
							_CJ.Users[i].AuthLevelName = "undefined";
						}
					}
				}

				lookup = _CJ.lookUps.Users[_CJ.Session.UserID];
				if (lookup > -1) {
					_CJ.Session.Authenticated = true;
					_CJ.Session.NickName = _CJ.Users[lookup].NickName;
					_CJ.Session.Avatar = _CJ.Users[lookup].AvatarID;
					_CJ.Session.AuthLevel = _CJ.Users[lookup].AuthLevel;
					_CJ.Session.isAdmin = (_CJ.Session.AuthLevel === 4096);
				}
			}

			if (_CJ.Overview && _CJ.Overview.length === 1) {
				_CJ.Overview = _CJ.Overview[0];
				_CJ.Overview.LastUpdate = new Date(_CJ.Overview.LastUpdate).toString();
			}


            if (_CJ.HostInformations && _CJ.HostInformations.length === 1) {
                _CJ.HostInformations = _CJ.HostInformations[0];
                _CJ.HostInformations.IPAddresses = _CJ.HostInformations.IPAddresses.split(';');
                for (let i = 0; i < _CJ.HostInformations.IPAddresses.length; i++) {
                    if (_CJ.HostInformations.IPAddresses[i].indexOf(':') > -1) {
                        // ipv6
                        _CJ.HostInformations.IPAddresses[i] = '[' + _CJ.HostInformations.IPAddresses[i] + ']';
                    }
                }
            }



			if (_CJ.State && _CJ.State.length === 1) {
				_CJ.State = _CJ.State[0];
				_CJ.State.isRunning = _CJ.State.State === 'Running';
				//_CJ.Overview.LastUpdate = new Date(_CJ.Overview.LastUpdate).toString();
			}

            if (_CJ.Crawlers) {
			    let c = {};
                for (let i = 0; i < _CJ.Crawlers.length; i++) {
                    c[_CJ.Crawlers[i].Name] = true;
                }
                _CJ.Crawlers = c;
            }

			if (_CJ.Progress) {
				if (_CJ.State.State === 'Stopped') {
					_CJ.Progress = undefined;
				} else {
					for (let i = 0; i < _CJ.Progress.length; i++) {
						_CJ.Progress[i].Percent = _CJ.Progress[i].Progress * 100;
						_CJ.Progress[i].PercentText = _CJ.Progress[i].Percent.toFixed(2).toString();
						if ((_CJ.Progress[i].Percent > 0) && (_CJ.Progress[i].Percent < 100)) {
							_CJ.Progress[i].Animated = 'progress-bar-striped progress-bar-animated';
						}
					}
				}
			}

			if (_CJ.PlayerStates && _CJ.PlayerStates.length > 0) {
				_CJ.pollHash = _CJ.PlayerStates[0].Hash;
			}


			if (_CJ.StreamSettings && _CJ.StreamSettings.length === 1) {
				_CJ.StreamSettings[0].MinimumLengthStr = getTimeStringFromMSLong(_CJ.StreamSettings[0].MinimumLength);
				_CJ.StreamSettings[0].MaximumLengthStr = getTimeStringFromMSLong(_CJ.StreamSettings[0].MaximumLength);
			}

			if (_CJ.Subsets && _CJ.StreamSettings) {
				for (let i = 0; i < _CJ.Subsets.length; i++) {
					if (_CJ.Subsets[i].ID === _CJ.StreamSettings[0].SubsetID) {
						_CJ.Subsets[i].isActive = true;
						_CJ.Subsets[i].activeClass = 'jb-bg-primary-5';
					} else {
						delete _CJ.Subsets[i].isActive;
						delete _CJ.Subsets[i].activeClass;
					}
				}
			}

			if (_CJ.SubsetFilters && _CJ.Params && _CJ.Params.id) {
				const lookup = _CJ.lookUps.Subsets[_CJ.Params.id];
				if (lookup > -1) {
					_CJ.activeSubset = _CJ.Subsets[lookup];
				}
			}


			if (_CJ.CombinedArtists) {
				for (let i = 0; i < _CJ.CombinedArtists.length; i++) {
					_CJ.CombinedArtists[i].DurationText = getTimeStringFromMS(_CJ.CombinedArtists[i].Duration);
					_CJ.CombinedArtists[i].GenreNames = splitItems(_CJ.CombinedArtists[i].Genres);
					_CJ.CombinedArtists[i].TagNames = splitItems(_CJ.CombinedArtists[i].Tags);
				}
			}

			if (_CJ.CombinedAlbums) {
				for (let i = 0; i < _CJ.CombinedAlbums.length; i++) {
					_CJ.CombinedAlbums[i].DurationText = getTimeStringFromMS(_CJ.CombinedAlbums[i].Duration);
					_CJ.CombinedAlbums[i].GenreNames = splitItems(_CJ.CombinedAlbums[i].Genres);
					_CJ.CombinedAlbums[i].TagNames = splitItems(_CJ.CombinedAlbums[i].Tags);
				}
			}

			if (_CJ.CombinedAudioFiles) {
                for (let i = 0; i < _CJ.CombinedAudioFiles.length; i++) {
					_CJ.CombinedAudioFiles[i].DurationText = getTimeStringFromMS(_CJ.CombinedAudioFiles[i].Duration);
					_CJ.CombinedAudioFiles[i].GenreNames = splitItems(_CJ.CombinedAudioFiles[i].Genres);
					_CJ.CombinedAudioFiles[i].TagNames = splitItems(_CJ.CombinedAudioFiles[i].Tags);
				}
			}


			//			_CJ.NowPlaying = (_CJ.NowPlaying && (_CJ.NowPlaying.length === 1) && (_CJ.NowPlaying[0].Duration > 0)) ? _CJ.NowPlaying[0] : false;

			if (Array.isArray(_CJ.NowPlaying)) {
				_CJ.NowPlaying = _CJ.NowPlaying[0];
			}

			if (_CJ.NowPlaying) {
				_CJ.NowPlaying.isPlaying = (_CJ.NowPlaying && (_CJ.NowPlaying.Duration > 0));

				if (_CJ.NowPlaying.isPlaying) {


					let lookup = _CJ.lookUps.AudioFiles[_CJ.NowPlaying.AudioFileID];
					if (lookup > -1) {
						_CJ.NowPlaying.AudioFile = _CJ.AudioFiles[lookup];
						_CJ.NowPlaying.UrlTitle = encodeURIComponent(_CJ.NowPlaying.AudioFile.Title);
					}
					lookup = _CJ.lookUps.Artists[_CJ.NowPlaying.AudioFile.SongArtistID];
					if (lookup > -1) {
						_CJ.NowPlaying.Artist = _CJ.Artists[lookup];
					}
					lookup = _CJ.lookUps.Albums[_CJ.NowPlaying.AudioFile.AlbumID];
					if (lookup > -1) {
						_CJ.NowPlaying.Album = _CJ.Albums[lookup];
					}
					_CJ.NowPlaying.GenreNames = splitItems(_CJ.NowPlaying.AudioFile.Genres);
					_CJ.NowPlaying.TagNames = splitItems(_CJ.NowPlaying.AudioFile.Tags);


					if (_CJ.PlayListItems && (_CJ.PlayListItems.length > 0)) {
						for (let i = 0; i < _CJ.PlayListItems.length; i++) {
							let lookupAudioFile = _CJ.lookUps.AudioFiles[_CJ.PlayListItems[i].AudioFileID];
							if (lookupAudioFile > -1) {
								_CJ.PlayListItems[i].AudioFile = _CJ.AudioFiles[lookupAudioFile];
								_CJ.PlayListItems[i].UrlTitle = encodeURIComponent(_CJ.PlayListItems[i].AudioFile.Title);
							
								let lookupArtist = _CJ.lookUps.Artists[_CJ.PlayListItems[i].AudioFile.SongArtistID];
								if (lookupArtist > -1) {
									_CJ.PlayListItems[i].Artist = _CJ.Artists[lookupArtist];
								}
								let lookupAlbum = _CJ.lookUps.Albums[_CJ.PlayListItems[i].AudioFile.AlbumID];
								if (lookupAlbum > -1) {
									_CJ.PlayListItems[i].Album = _CJ.Albums[lookupAlbum];
								}
								_CJ.PlayListItems[i].DurationText = getTimeStringFromMS(_CJ.PlayListItems[i].AudioFile.Duration);
								_CJ.PlayListItems[i].GenreNames = splitItems(_CJ.PlayListItems[i].AudioFile.Genres);
								_CJ.PlayListItems[i].TagNames = splitItems(_CJ.PlayListItems[i].AudioFile.Tags);

							}
							
							let lookupUser = _CJ.lookUps.Users[_CJ.PlayListItems[i].OwnerID];
							if (lookupUser > -1) {
								_CJ.PlayListItems[i].Owner = _CJ.Users[lookupUser];
							} else {
								_CJ.PlayListItems[i].Owner = [];
								_CJ.PlayListItems[i].Owner.AvatarID = 0;
								if (_CJ.Subsets.length > 0) {
									_CJ.PlayListItems[i].Owner.NickName = "Subset " + _CJ.Subsets[0].Name;
								} else {
									_CJ.PlayListItems[i].Owner.NickName = "Random selection";
								}
							}
							_CJ.PlayListItems[i].canRemove = (_CJ.Session.Authenticated &&
								((_CJ.PlayListItems[i].OwnerID === _CJ.Session.UserID) ||
									((_CJ.PlayListItems[i].OwnerID === '0')) ||
									_CJ.Session.isAdmin));

						}
					}
				}
			}

			const convertSelectorData = function (name, copyIDs) {
				let t = _CJ[name];
				if (t) {
					let res = [];
					for (let i = 0; i < t.length; i++) {
						res.push({
							id: copyIDs ? t[i].ID : t[i].Name,
							text: t[i].Name
						});
					}
					res.show = (res.length > 1);
					_CJ[name] = res;
				}
			};

			convertSelectorData('Categories', true);
			convertSelectorData('Genres');
			convertSelectorData('Tags');

			_CJ.SelectorData = {
				categories: {
					data: _CJ.Categories,
					placeholder: 'Category',
					allowClear: false,
					pName: 'c'
				},
				genres: {
					data: _CJ.Genres,
					placeholder: 'Genre',
					tags: true,
					allowClear: false,
					pName: 'g'
				},
				tags: {
					data: _CJ.Tags,
					placeholder: 'Tag',
					tags: true,
					allowClear: false,
					pName: 't'
				}
			};

			if (typeof _CJ.clientHook === 'function') {
				_CJ.clientHook();
			}
			window.console.log(_CJ);
		};

		_CJ.parseNewData(window.CaveJSON);

		return _CJ;
	}
		()
	);
}
