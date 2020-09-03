// QRMYKEYS.cpp : main func 4 create qr code of private and public keys.
//

#include "pch.h"
#include <iostream>
#include <cstdint>
#include <cstdlib>
#include <cstring>
#include <iostream>
#include <string>
#include <vector>
#include "QrCode.hpp"
#include <fstream>
#include <Windows.h>

using std::uint8_t;
using qrcodegen::QrCode;
using qrcodegen::QrSegment;


// Function prototypes

static void PrintPrivateKey(std::string);
static void PrintPublicKey(std::string);
static std::string ExePath();

// The main application program.
int main() {
	
	std::string exepath = ExePath();
	PrintPrivateKey(exepath);
	PrintPublicKey(exepath);
	return EXIT_SUCCESS;
}


static void PrintPrivateKey(std::string directory_path) {

	std::string file_path = directory_path + "\\privateKey";
	std::string output_path = directory_path + "\\QRprivatekey.svg";
	const QrCode::Ecc errCorLvl = QrCode::Ecc::LOW;  // Error correction level
	std::ifstream instream(file_path, std::ios::in | std::ios::binary);
	std::vector<uint8_t> data((std::istreambuf_iterator<char>(instream)), std::istreambuf_iterator<char>());
	std::cout << "0. size: " << data.size() << '\n';
	// Make and print the QR Code symbol
	const QrCode qr = QrCode::encodeBinary(data, errCorLvl);
	std::ofstream fs(output_path);

	if (!fs)
	{
		std::cerr << "Cannot open the output file." << std::endl;
		return;
	}
	fs << qr.toSvgString(4);
	fs.close();
}
static void PrintPublicKey(std::string directory_path) {

	std::string file_path = directory_path + "\\publicKey";
	std::string output_path = directory_path + "\\QRpublicKey.svg";
	const QrCode::Ecc errCorLvl = QrCode::Ecc::LOW;  // Error correction level
	std::ifstream instream(file_path, std::ios::in | std::ios::binary);
	std::vector<uint8_t> data((std::istreambuf_iterator<char>(instream)), std::istreambuf_iterator<char>());
	std::cout << "0. size: " << data.size() << '\n';
	// Make and print the QR Code symbol
	const QrCode qr = QrCode::encodeBinary(data, errCorLvl);
	std::ofstream fs(output_path);

	if (!fs)
	{
		std::cerr << "Cannot open the output file." << std::endl;
		return;
	}
	fs << qr.toSvgString(4);
	fs.close();
}

static std::string ExePath()
{
	using namespace std;

	char buffer[MAX_PATH];

	GetModuleFileNameA(NULL, buffer, MAX_PATH);

	string::size_type pos = string(buffer).find_last_of("\\/");

	if (pos == string::npos)
	{
		return "";
	}
	else
	{
		return string(buffer).substr(0, pos);
	}
}
