using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace AddressCode
{
	class CodeDictionary
	{
		private JArray AliasArray;

		/* 각 단계별로 3개의 Dictionary 생성
		 * CodeToAddr는 Key로 코드, Value로 주소
		 * AddrToCode는 Key로 주소, Value로 코드
		 * PartAddr는 Key로 부분 주소(예: 주소가 서울특별시면 서울, 울특, 특별, 별시, 서울특, ...), Value로 코드
		 */
		public Dictionary<int, string> SidoDict_CodeToAddr { get; } // 시, 도
		public Dictionary<int, string> SGGDict_CodeToAddr { get; } // 시, 군, 구
		public Dictionary<int, string> EMDDict_CodeToAddr { get; } // 읍, 면, 동
		public Dictionary<int, Dictionary<int, string>> RiDict_CodeToAddr { get; } // 리, 다른 Dict과 다르게 key가 읍면동 key, value가 해당 읍면동 하위의 리 Dict

		public Dictionary<string, List<int>> SidoDict_AddrToCode { get; } // 시, 도
		public Dictionary<string, List<int>> SGGDict_AddrToCode { get; } // 시, 군, 구
		public Dictionary<string, List<int>> EMDDict_AddrToCode { get; } // 읍, 면, 동
		public Dictionary<string, List<long>> RiDict_AddrToCode { get; } // 리, CodeToAddr의 리와 다르게 전체 코드를 사용

		public Dictionary<string, List<int>> SidoDict_PartAddr { get; } // 시, 도
		public Dictionary<string, List<int>> SGGDict_PartAddr { get; } // 시, 군, 구
		public Dictionary<string, List<int>> EMDDict_PartAddr { get; } // 읍, 면, 동
		public Dictionary<string, List<long>> RiDict_PartAddr { get; } // 리

		public CodeDictionary(string filePath)
		{
			AliasNameInitialize();

			SidoDict_CodeToAddr = new Dictionary<int, string>();
			SGGDict_CodeToAddr = new Dictionary<int, string>();
			EMDDict_CodeToAddr = new Dictionary<int, string>();
			RiDict_CodeToAddr = new Dictionary<int, Dictionary<int, string>>();

			SidoDict_AddrToCode = new Dictionary<string, List<int>>();
			SGGDict_AddrToCode = new Dictionary<string, List<int>>();
			EMDDict_AddrToCode = new Dictionary<string, List<int>>();
			RiDict_AddrToCode = new Dictionary<string, List<long>>();

			SidoDict_PartAddr = new Dictionary<string, List<int>>();
			SGGDict_PartAddr = new Dictionary<string, List<int>>();
			EMDDict_PartAddr = new Dictionary<string, List<int>>();
			RiDict_PartAddr = new Dictionary<string, List<long>>();

			try
			{
				// StreamReader로 파일 읽기
				StreamReader codeFile = new StreamReader(filePath, Encoding.GetEncoding(949));

				// 파일을 한 줄씩 읽어서 line에 저장
				string line;
				while((line = codeFile.ReadLine()) != null)
				{
					// 제일 첫 줄이면 그냥 통과
					if (line.StartsWith("법정동코드"))
						continue;

					// 폐지된 코드면 그냥 통과
					if (line.EndsWith("폐지"))
						continue;

					/*
					 * line은 "법정동코드\t법정동명\t폐지여부"로 구성되어있음
					 * '\t'를 기준으로 split해 법정동코드와 법정동명을 구함
					 */
					string[] parsedLine = line.Split('\t');

					/* 
					 * 법정동코드에서 시도/시군구/읍면동/리 코드 추출
					 * 코드가 1234567890일 경우,
					 * 시도: 12000, 시군구: 12345, 읍면동: 12345678, 리: 90
					 */
					try
					{
						long fullCode = long.Parse(parsedLine[0]);
						int sidoCode = (int)(fullCode / 100000000 * 1000);
						int sggCode = (int)(fullCode / 100000);
						int emdCode = (int)(fullCode / 100);
						int riCode = (int)(fullCode % 100);
					

						/*
						 * 법정동명은 ' '로 각 단계를 나누고 있음
						 * ex) 서울특별시 동작구 사당동
						 * 법정동명을 ' '를 기준으로 split하면 시도/시군구/읍면동/리로 나눌 수 있음
						 */
						string[] parsedAddress = parsedLine[1].Split(' ');

						/*
						 * 세종특별자치시는 시·도에 포함되지만 5자리 코드(36110)를 사용하고 하위에 바로 읍·면·동을 가짐
						 * 따라서 시·도와 시·군·구 모두 코드 36110, 주소 "세종특별자치시"로 중복 저장함
						 */
						if (parsedAddress[0] == "세종특별자치시")
						{
							// 시도 코드와 시군구 코드 모두 36110으로 저장
							sidoCode = sggCode;

							// 기본 데이터에 세종특별자치시가 중복 표기된 경우도 존재하기 때문에 조건문 필요
							if (parsedAddress.Length == 1 || parsedAddress[1] != "세종특별자치시")
							{
								// 법정동명 array를 list로 변환해 시군구 주소에 세종특별자치시 삽입 후 다시 array로 변환
								List<string> addressList = parsedAddress.ToList();
								addressList.Insert(1, "세종특별자치시");
								parsedAddress = addressList.ToArray();
							}
						}

						/*
						 * 시 하위구역으로 존재하는 구 처리 ex) 경기도 수원시 장안구 파장동
						 * 현재는 ' '를 기준으로 split했기 때문에 경기도/수원시/장안구/파장동 순서로 저장되어 있음
						 * 이걸 경기도/수원시 장안구/파장동으로 수정
						 */
						if (parsedAddress.Length >= 3 && parsedAddress[2].EndsWith("구"))
						{
							// 법정동명 array를 list로 변환
							List<string> addressList = parsedAddress.ToList();
							// 시와 구를 한 string으로 합쳐 시군구 주소에 삽입
							addressList.Insert(1, addressList[1] + " " + addressList[2]);
							// 기존에 있던 시 주소와 구 주소 삭제
							addressList.RemoveAt(3);
							addressList.RemoveAt(2);
							// list를 다시 array로 변환
							parsedAddress = addressList.ToArray();
						}

						// 리에서 한자표기 제거
						if (parsedAddress.Length >= 4 && parsedAddress[3].Contains("("))
						{
							// 기암리(岐岩) 처럼 한자표기가 맨 뒤에 오므로 '(' 이전까지 Substring
							parsedAddress[3] = parsedAddress[3].Substring(0, parsedAddress[3].IndexOf('('));
						}

						/*
						 * 각 코드와 주소를 알맞은 Dictionary에 저장
						 */

						// 시, 도
						// CodeToAddr
						if(!SidoDict_CodeToAddr.ContainsKey(sidoCode))
							SidoDict_CodeToAddr.Add(sidoCode, parsedAddress[0]);

						// AddrToCode
						if (!SidoDict_AddrToCode.ContainsKey(parsedAddress[0]))
							SidoDict_AddrToCode.Add(parsedAddress[0], new List<int>());

						if (!SidoDict_AddrToCode[parsedAddress[0]].Contains(sidoCode))
							SidoDict_AddrToCode[parsedAddress[0]].Add(sidoCode);

						// AliasName 삽입
						foreach (JToken aliasName in AliasArray)
						{
							string name = (string)aliasName["Name"];
							string alias = (string)aliasName["Alias"];

							if (name == parsedAddress[0])
							{
								if (!SidoDict_AddrToCode.ContainsKey(alias))
									SidoDict_AddrToCode.Add(alias, new List<int>());

								if (!SidoDict_AddrToCode[alias].Contains(sidoCode))
									SidoDict_AddrToCode[alias].Add(sidoCode);
							}
						}

						// PartAddr
						for (int partLength = 2; partLength < parsedAddress[0].Length; partLength++)
						{
							for (int start = 0; start <= parsedAddress[0].Length - partLength; start++)
							{
								string partAddr = parsedAddress[0].Substring(start, partLength);

								if (!SidoDict_PartAddr.ContainsKey(partAddr))
									SidoDict_PartAddr.Add(partAddr, new List<int>());

								if (!SidoDict_PartAddr[partAddr].Contains(sidoCode))
									SidoDict_PartAddr[partAddr].Add(sidoCode);
							}
						}

						// 시, 군, 구
						if (parsedAddress.Length >= 2)
						{
							// CodeToAddr
							if (!SGGDict_CodeToAddr.ContainsKey(sggCode))
								SGGDict_CodeToAddr.Add(sggCode, parsedAddress[1]);

							// 주소를 Key로 사용할 때 시와 구가 같이 들어있는 경우(예: 경기도 수원시 팔달구), 시는 버리고 구만 Key로 사용함
							string[] parsedSGG = parsedAddress[1].Split(' ');
							if (parsedSGG.Length > 1)
								parsedAddress[1] = parsedSGG[1];
								
							// AddrToCode
							if (!SGGDict_AddrToCode.ContainsKey(parsedAddress[1]))
								SGGDict_AddrToCode.Add(parsedAddress[1], new List<int>());

							if (!SGGDict_AddrToCode[parsedAddress[1]].Contains(sggCode))
								SGGDict_AddrToCode[parsedAddress[1]].Add(sggCode);

							// PartAddr
							for (int partLength = 2; partLength < parsedAddress[1].Length; partLength++)
							{
								for (int start = 0; start <= parsedAddress[1].Length - partLength; start++)
								{
									string partAddr = parsedAddress[1].Substring(start, partLength);

									if (!SGGDict_PartAddr.ContainsKey(partAddr))
										SGGDict_PartAddr.Add(partAddr, new List<int>());

									if (!SGGDict_PartAddr[partAddr].Contains(sggCode))
										SGGDict_PartAddr[partAddr].Add(sggCode);
								}
							}
						}

						// 읍, 면, 동
						if (parsedAddress.Length >= 3)
						{
							// CodeToAddr
							if (!EMDDict_CodeToAddr.ContainsKey(emdCode))
								EMDDict_CodeToAddr.Add(emdCode, parsedAddress[2]);

							// AddrToCode
							if (!EMDDict_AddrToCode.ContainsKey(parsedAddress[2]))
								EMDDict_AddrToCode.Add(parsedAddress[2], new List<int>());

							if (!EMDDict_AddrToCode[parsedAddress[2]].Contains(emdCode))
								EMDDict_AddrToCode[parsedAddress[2]].Add(emdCode);

							// PartAddr
							for (int partLength = 2; partLength < parsedAddress[2].Length; partLength++)
							{
								for (int start = 0; start <= parsedAddress[2].Length - partLength; start++)
								{
									string partAddr = parsedAddress[2].Substring(start, partLength);

									if (!EMDDict_PartAddr.ContainsKey(partAddr))
										EMDDict_PartAddr.Add(partAddr, new List<int>());

									if (!EMDDict_PartAddr[partAddr].Contains(emdCode))
										EMDDict_PartAddr[partAddr].Add(emdCode);
								}
							}
						}

						// 리
						if (parsedAddress.Length >= 4)
						{
							// CodeToAddr
							// 다른 Dictionary들과 다르게 Key로 emdCode를 사용하고, Value는 <riCode, 주소>의 Dictionary
							if (!RiDict_CodeToAddr.ContainsKey(emdCode))
								RiDict_CodeToAddr.Add(emdCode, new Dictionary<int, string>());
							
							RiDict_CodeToAddr[emdCode].Add(riCode, parsedAddress[3]);

							// AddrToCode
							// 다른 Dictionary들과 다르게 Value에 전체코드를 사용
							if (!RiDict_AddrToCode.ContainsKey(parsedAddress[3]))
								RiDict_AddrToCode.Add(parsedAddress[3], new List<long>());

							if (!RiDict_AddrToCode[parsedAddress[3]].Contains(fullCode))
								RiDict_AddrToCode[parsedAddress[3]].Add(fullCode);

							// PartAddr
							for (int partLength = 2; partLength < parsedAddress[3].Length; partLength++)
							{
								for (int start = 0; start <= parsedAddress[3].Length - partLength; start++)
								{
									string partAddr = parsedAddress[3].Substring(start, partLength);

									if (!RiDict_PartAddr.ContainsKey(partAddr))
										RiDict_PartAddr.Add(partAddr, new List<long>());

									if (!RiDict_PartAddr[partAddr].Contains(fullCode))
										RiDict_PartAddr[partAddr].Add(fullCode);
								}
							}
						}
					}
					catch (FormatException)
					{
						Console.WriteLine("코드 분석에 실패했습니다 : " + parsedLine[0]);
					}
				}
			}
			catch(FileNotFoundException)
			{
				Console.WriteLine("해당 파일을 찾을 수 없습니다 : " + filePath);
			}
		}

		private void AliasNameInitialize()
		{
			AliasArray = new JArray()
			{
				new AliasName("서울특별시", "서울시").ToJson(),
				new AliasName("부산광역시", "부산시").ToJson(),
				new AliasName("대구광역시", "대구시").ToJson(),
				new AliasName("인천광역시", "인천시").ToJson(),
				new AliasName("광주광역시", "광주시").ToJson(),
				new AliasName("대전광역시", "대전시").ToJson(),
				new AliasName("울산광역시", "울산시").ToJson(),
				new AliasName("세종특별자치시", "세종시").ToJson(),
				new AliasName("충청북도", "충북").ToJson(),
				new AliasName("충청남도", "충남").ToJson(),
				new AliasName("전라북도", "전북").ToJson(),
				new AliasName("전라남도", "전남").ToJson(),
				new AliasName("경상북도", "경북").ToJson(),
				new AliasName("경상남도", "경남").ToJson(),
				new AliasName("제주특별자치도", "제주도").ToJson()
			};
		}
		public string SearchAddress(long code)
		{
			// 입력코드로 각 단계별 Key값 생성
			int sidoCode = (int)(code / 100000000 * 1000);
			int sggCode = (int)(code / 100000);
			int emdCode = (int)(code / 100);
			int riCode = (int)(code % 100);

			// 시·군·구 코드가 36110이면 시·도 코드도 36110으로
			if (sggCode == 36110)
				sidoCode = sggCode;

			// 검색 결과를 저장할 StringBuilder
			StringBuilder sb = new StringBuilder();

			// 각 Dict에서 검색해서 Value를 가져와 sb에 붙임
			try
			{
				// 시도 주소는 무조건 존재함
				sb.Append(SidoDict_CodeToAddr[sidoCode]);

				// 시군구 코드 중 마지막 3자리가 0이 아니면 시군구 주소가 존재함
				if (sggCode % 1000 != 0 & sggCode != 36110) // 36110: 세종특별자치시, 내용이 시도와 중복되니 생략
					sb.Append(" " + SGGDict_CodeToAddr[sggCode]);

				// 읍면동 코드 중 마지막 3자리가 0이 아니면 읍면동 주소가 존재함
				if (emdCode % 1000 != 0)
					sb.Append(" " + EMDDict_CodeToAddr[emdCode]);

				// 리 코드가 0이 아니면 리 주소가 존재함
				if (riCode != 0)
					sb.Append(" " + RiDict_CodeToAddr[emdCode][riCode]);
			}
			catch(KeyNotFoundException)
			{
				sb.Clear();
				sb.Append("검색 결과가 없습니다.");
			}

			return sb.ToString();
		}


		public List<string> SearchCode(string address, int searchOption)
		{
			// 모든 검색결과를 저장할 List
			List<string> result = new List<string>();

			// 완전일치 검색
			result.AddRange(SearchCodeWithFullAddr(address, out bool fullAddrFlag));

			// 부분일치 검색
			result.AddRange(SearchCodeWithPartAddr(address, out bool partAddrFlag, searchOption));

			// 두 검색 모두 결과 flag가 false일 경우 검색 결과 없음
			if (!fullAddrFlag && !partAddrFlag)
			{
				result.Clear();
			}

			// 검색 결과가 없을 경우 특수 문자열 저장
			if (result.Count == 0)
				result.Add("검색 결과가 없습니다.\n");

			return result;
		}

		private bool AliasContainsName(string name)
		{
			foreach (JToken json in AliasArray)
			{
				if ((string)json["Alias"] == name)
					return true;
			}

			return false;
		}

		private List<string> SearchCodeWithFullAddr(string address, out bool resultFlag)
		{
			// 검색 결과 저장 List
			List<string> result = new List<string>();

			

			/*
			 * 입력 주소 끝부분을 기준으로 행정구역 단계를 판정
			 * 1: 특별시, 광역시, 자치시, 도, AliasName
			 * 2: 시, 군, 구
			 * 3: 읍, 면, 동, 가(예: 종로2가), 로(예: 세종로)
			 * 4: 리
			 */

			int addressLevel;
			if (address.EndsWith("특별시") || address.EndsWith("광역시") || address.EndsWith("자치시") || address.EndsWith("도") || AliasContainsName(address))
				addressLevel = 1;
			else if (address.EndsWith("시") || address.EndsWith("군") || address.EndsWith("구"))
				addressLevel = 2;
			else if (address.EndsWith("읍") || address.EndsWith("면") || address.EndsWith("동") || address.EndsWith("가") || address.EndsWith("로"))
				addressLevel = 3;
			else if (address.EndsWith("리"))
				addressLevel = 4;
			else
				addressLevel = 0;

			try
			{
				/* 
				 * 입력 주소의 단계에 해당하는 Dict_AddrToCode에서 코드 List 획득
				 * 획득한 List로 SearchAddress(), 전체 주소 획득
				 * 전체 주소를 result에 Add
				 */
				if (addressLevel == 0)
				{
					resultFlag = false;
				}
				else
				{
					// 시, 도
					if (addressLevel == 1)
					{
						List<int> codeList = SidoDict_AddrToCode[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							long fullCode = (long)codeList[i] * 100000;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}
					}
					// 시, 군, 구
					else if (addressLevel == 2)
					{
						List<int> codeList = SGGDict_AddrToCode[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							long fullCode = (long)codeList[i] * 100000;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}
						
					}
					// 읍, 면, 동
					else if (addressLevel == 3)
					{
						List<int> codeList = EMDDict_AddrToCode[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							long fullCode = (long)codeList[i] * 100;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}
					}
					// 리
					else
					{
						List<long> codeList = RiDict_AddrToCode[address];

						for (int i = 0; i < codeList.Count; i++)
							result.Add(codeList[i] + " \n" + SearchAddress(codeList[i]) + "\n");
					}

					resultFlag = true;
				}
			}
			catch (KeyNotFoundException)
			{
				resultFlag = false;
			}

			return result;
		}

		private List<string> SearchCodeWithPartAddr(string address, out bool resultFlag, int searchOption)
		{
			// 검색 결과 저장 List
			List<string> result = new List<string>();

			// 검색 옵션이 없을 경우 검색하지 않음
			if (searchOption == 0)
			{
				resultFlag = false;
			}
			else
			{
				// 검색 옵션. true면 해당 Dictionary에서 검색을 시도
				bool sidoOption = false, sggOption = false, emdOption = false, riOption = false;

				// 검색 결과. true면 해당 Dictionary에서 검색을 성공함
				bool sidoResult = false, sggResult = false, emdResult = false, riResult = false;

				// 검색 옵션값 설정
				if (searchOption >= 8)
				{
					sidoOption = true;
					searchOption -= 8;
				}
				if (searchOption >= 4)
				{
					sggOption = true;
					searchOption -= 4;
				}
				if (searchOption >= 2)
				{
					emdOption = true;
					searchOption -= 2;
				}
				if (searchOption >= 1)
					riOption = true;

				/*
				 * 각 Dictionary마다 검색 요청이 있으면 검색
				 * KeyNotFoundException 발생시 해당 검색 결과 flag를 false로
				 * 검색 성공시 검색 결과 flag를 true로
				 */

				// 시·도
				if (sidoOption)
				{
					try
					{
						List<int> codeList = SidoDict_PartAddr[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							long fullCode = (long)codeList[i] * 100000;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}

						sidoResult = true;
					}
					catch (KeyNotFoundException)
					{
						sidoResult = false;
					}
				}

				// 시·군·구
				if (sggOption)
				{
					try
					{
						List<int> codeList = SGGDict_PartAddr[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							// 세종특별자치시는 시·군·구 검색에서는 나오지 않음
							if (codeList[i] == 36110)
								continue;

							long fullCode = (long)codeList[i] * 100000;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}

						sggResult = true;
					}
					catch (KeyNotFoundException)
					{
						sggResult = false;
					}
				}

				// 읍·면·동
				if (emdOption)
				{
					try
					{
						List<int> codeList = EMDDict_PartAddr[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							long fullCode = (long)codeList[i] * 100;
							result.Add(fullCode + " \n" + SearchAddress(fullCode) + "\n");
						}

						emdResult = true;
					}
					catch (KeyNotFoundException)
					{
						emdResult = false;
					}
				}

				// 리
				if (riOption)
				{
					try
					{
						List<long> codeList = RiDict_PartAddr[address];

						for (int i = 0; i < codeList.Count; i++)
						{
							result.Add(codeList[i] + " \n" + SearchAddress(codeList[i]) + "\n");
						}

						riResult = true;
					}
					catch (KeyNotFoundException)
					{
						riResult = false;
					}
				}

				// 모든 검색 결과 flag가 false일 경우 검색 결과 없음
				if (sidoResult || sggResult || emdResult || riResult)
				{
					resultFlag = true;
				}
				else
				{
					resultFlag = false;
				}
			}

			return result;
		}
	}
}
